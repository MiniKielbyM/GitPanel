
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Unity.VisualScripting;

public static class GitUtils
{
    public static bool IsGitRepository(string path = null)
    {
        // Use the root of the Unity project if no path is provided
        if (string.IsNullOrEmpty(path))
        {
            path = Directory.GetCurrentDirectory();
        }

        string gitFolderPath = Path.Combine(path, ".git");
        return Directory.Exists(gitFolderPath);
    }
    public static bool HasGitHubRemote(string path = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(Path.Combine(path, ".git")))
        {
            return false; // Not a Git repo
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "remote -v", // shows remotes with URLs
                WorkingDirectory = path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains("github.com");
            }
        }
        catch
        {
            return false;
        }
    }
    public static async Task<string> GetGitHubUsername(string accessToken)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UnityGitPanel");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await client.GetAsync("https://api.github.com/user");

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(result, "\"login\"\\s*:\\s*\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"❌ Failed to fetch username: {response.StatusCode}");
            }
        }
        return null;
    }
    private static void RunGitCommand(string args, string workingDirectory = null)
    {
        if (string.IsNullOrEmpty(workingDirectory))
        {
            workingDirectory = Directory.GetCurrentDirectory();
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"❌ Git command failed: {error}");
            }
            else
            {
                UnityEngine.Debug.Log($"✅ Git command succeeded: {output}");
            }
        }
    }
    public static async Task<string> CreateRepo(string repoName, string accessToken)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UnityGitPanel");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var json = $"{{\"name\": \"{repoName}\", \"private\": false}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.github.com/user/repos", content);
            string result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                UnityEngine.Debug.LogError($"❌ Failed to create repo: {response.StatusCode} - {result}");
                return null;
            }

            string username = await GetGitHubUsername(accessToken);
            if (string.IsNullOrEmpty(username))
            {
                UnityEngine.Debug.LogError("❌ Could not fetch GitHub username.");
                return null;
            }

            string remoteUrl = $"https://github.com/{username}/{repoName}.git";
            string root = Directory.GetCurrentDirectory();
            string gitignorePath = Path.Combine(root, ".gitignore");

            // Ensure Unity patterns are present in .gitignore
            string[] unityPatterns = new string[]
            {
            "[Ll]ibrary/",
            "[Tt]emp/",
            "[Oo]bj/",
            "[Bb]uild/",
            "[Bb]uilds/",
            "[Ll]ogs/",
            "[Mm]emoryCaptures/",
            "sysinfo.txt",
            "*.userprefs",
            "*.csproj",
            "*.unityproj",
            "*.sln",
            "*.suo",
            "*.tmp",
            "*.user",
            "*.booproj",
            "*.pidb",
            "*.svd",
            "*.pdb",
            "*.mdb",
            "*.opendb",
            "*.VC.db",
            ".vscode/",
            ".idea/",
            ".DS_Store",
            "*.apk",
            "*.aab"
            };

            HashSet<string> existingLines = new HashSet<string>();
            if (File.Exists(gitignorePath))
            {
                var lines = File.ReadAllLines(gitignorePath);
                foreach (var line in lines)
                    existingLines.Add(line.Trim());
            }

            using (StreamWriter writer = new StreamWriter(gitignorePath, append: true))
            {
                foreach (var pattern in unityPatterns)
                {
                    if (!existingLines.Contains(pattern))
                    {
                        writer.WriteLine(pattern);
                    }
                }
            }

            // Initialize local git repo if it doesn't exist
            if (!Directory.Exists(Path.Combine(root, ".git")))
            {
                RunGitCommand("init", root);
            }

            RunGitCommand($"remote add origin {remoteUrl}", root);
            RunGitCommand("add .gitignore", root);
            RunGitCommand("commit -m \"Ensure .gitignore is present and updated\"", root);
            RunGitCommand("branch -M main", root);
            RunGitCommand("push -u origin main", root);

            return remoteUrl;
        }
    }

}
public class GitPanelWindow : EditorWindow
{
    private string commitMessage = "";
    private string[] changedFiles = new string[0];
    private double lastRefreshTime;
    private double refreshInterval = 5.0;

    private enum PushState { Idle, Pushing, Success, Error }
    private PushState pushStatus = PushState.Idle;
    private string lastPushMessage = "";
    private double lastPushTime;

    [MenuItem("Window/Git Panel")]
    public static void ShowWindow()
    {
        GetWindow<GitPanelWindow>("Git Panel");
    }

    private void OnEnable()
    {
        RefreshGitStatus();
    }

    private void OnGUI()
    {
        GUILayout.Label("Changed Files:", EditorStyles.boldLabel);

        foreach (var file in changedFiles)
        {
            GUILayout.Label(file);
        }

        GUILayout.Space(10);
        GUILayout.Label("Commit Message:", EditorStyles.boldLabel);
        commitMessage = EditorGUILayout.TextField(commitMessage);

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✓ Commit", GUILayout.Height(25)))
        {
            if (!string.IsNullOrWhiteSpace(commitMessage))
            {
                CommitChanges(commitMessage);
                commitMessage = "";
            }
            else
            {
                UnityEngine.Debug.LogWarning("⚠️ Commit message cannot be empty.");
            }
        }

        GUI.enabled = GitUtils.HasGitHubRemote();
        if (GUILayout.Button(GetPushButtonContent(), GUILayout.Width(80)))
        {
            PushToGitHub();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private GUIContent GetPushButtonContent()
    {
        string label = "Push";
        Texture icon = EditorGUIUtility.IconContent("CloudConnect").image;

        switch (pushStatus)
        {
            case PushState.Pushing:
                label = "Pushing...";
                icon = EditorGUIUtility.IconContent("RotateTool").image;
                break;
            case PushState.Success:
                label = "✓ Pushed";
                icon = EditorGUIUtility.IconContent("Collab").image;
                break;
            case PushState.Error:
                label = "Retry";
                icon = EditorGUIUtility.IconContent("console.erroricon").image;
                break;
        }

        return new GUIContent(label, icon);
    }

    private void RefreshGitStatus()
    {
        changedFiles = RunGitCommand("status --porcelain").Split('\n');
    }

    private void CommitChanges(string message)
    {
        RunGitCommand("add .");
        RunGitCommand($"commit -m \"{message}\"");
        RefreshGitStatus();
    }

    private async void PushToGitHub()
    {
        pushStatus = PushState.Pushing;
        Repaint();

        await Task.Run(() =>
        {
            try
            {
                RunGitCommand("push");
                pushStatus = PushState.Success;
                lastPushMessage = "✅ Changes pushed to GitHub.";
            }
            catch (System.Exception e)
            {
                pushStatus = PushState.Error;
                lastPushMessage = $"❌ Push failed: {e.Message}";
            }
        });

        lastPushTime = EditorApplication.timeSinceStartup;
        UnityEngine.Debug.Log(lastPushMessage);
        Repaint();
    }

    private void Update()
    {
        if (EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
        {
            RefreshGitStatus();
            Repaint();
            lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        if (pushStatus == PushState.Success || pushStatus == PushState.Error)
        {
            if (EditorApplication.timeSinceStartup - lastPushTime > 3.0)
            {
                pushStatus = PushState.Idle;
                Repaint();
            }
        }
    }

    private string RunGitCommand(string command)
    {
        var processInfo = new ProcessStartInfo("git", command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        using (var process = Process.Start(processInfo))
        {
            string result = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new System.Exception(error);
            }

            return result.Trim();
        }
    }
}
#endif