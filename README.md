# GitPane: GitHub Integration for Unity Editor

GitPane is a Unity Editor extension that provides direct access to Git and GitHub functionality from within the Unity Editor. It supports OAuth-based GitHub authentication and common Git operations such as commit, push, and pull, allowing developers to manage version control workflows without leaving the Unity environment.

## Features

- OAuth2 login with GitHub using a local redirect server
- Stores access token securely using `EditorPrefs`
- Auto-initialization of Git repository with optional push to GitHub
- GitHub repository creation via API
- Automatic `.gitignore` file generation tailored for Unity projects
- Commit functionality with staged file display
- Push and pull operations with branch tracking
- Status display showing local and remote changes
- Scene save verification before commits
- Works across Windows and macOS
- Minimal external dependencies (uses Unity's built-in networking and `System.Diagnostics.Process` for Git commands)

## Setup

### 1. Installation

Place `GitPane.cs` inside an `Editor` folder in your Unity project, for example:

Assets/Editor/GitPane.cs

markdown
Copy
Edit

### 2. GitHub OAuth Configuration

GitPane uses GitHub OAuth for authentication. You must register an OAuth app on GitHub:

- **Client ID**: `Ov23livN2pwLZuwTaP4Q`
- **Redirect URI**: `http://localhost:4567/callback`

These values are hardcoded in the script. If needed, update them to match your own registered application.

### 3. Requirements

- Unity 2020.3 or later
- Git installed and accessible from the system's PATH
- Internet access to communicate with GitHub API

## Usage

### Authentication

Select `GitPane > Sign in with GitHub`. A browser window will open, and once authorized, your access token will be stored using `EditorPrefs`.

### Repository Initialization

If your Unity project is not yet a Git repository, GitPane will prompt you to initialize it. You can also choose to create and push to a new GitHub repository directly from the UI.

### Committing Changes

After making changes in your project, enter a commit message in GitPane and press **Commit**. The tool verifies whether the active scene has been saved to avoid committing unsaved changes.

### Push and Pull

Use the **Push** and **Pull** buttons to synchronize with the remote GitHub repository. Status information is displayed for both local and remote states.

## Git Ignore Defaults

On initialization, GitPane generates a `.gitignore` file with Unity-recommended entries, including:

[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
*.userprefs
*.csproj
*.sln
.vscode/
.idea/
.DS_Store
*.apk
*.unitypackage
*.log

pgsql
Copy
Edit

## Token Storage

The GitHub personal access token is stored via Unity's `EditorPrefs` using the key:

GitHubAccessToken

pgsql
Copy
Edit

## Contributing

Feel free to fork the project or submit pull requests. The current implementation is single-file, modular, and designed for easy extension or integration into larger Unity tooling systems.

## License

MIT License. You are free to use, modify, and distribute this code. Attribution is optional but appreciated.

## Author

Developed by Phoebe (Software Design). Focused on integrating modern development tools into game engines and real-time applications.
