# Unity MCP Server — Setup & Use

## Is it installed?

**Yes.** The Unity MCP package (`com.unity.ai.mcp`) is bundled inside the
`com.unity.ai.assistant` package that this project already depends on. Its
modules are present in the package cache:

```
Library/PackageCache/com.unity.ai.assistant@6fee27370e6a/Modules/Unity.AI.MCP.Editor
Library/PackageCache/com.unity.ai.assistant@6fee27370e6a/Modules/Unity.AI.MCP.Runtime
```

…and the matching `.csproj` files (`Unity.AI.MCP.Editor.csproj`,
`Unity.AI.MCP.Runtime.csproj`) are already generated. No change to
`Packages/manifest.json` is required.

> If you ever need to install it into a *different* project: open
> **Window → Package Manager → + → Add package by name…** and enter
> `com.unity.ai.mcp` (or use the Git URL
> `https://github.com/Unity-Technologies/unity-mcp.git`).

## Start the MCP server

1. Open this project in **Unity 6000.3.19f1**.
2. In the menu choose **Window → Unity AI → Open MCP Console**.
3. The console shows the MCP server running (it turns the Unity Editor into
   an MCP server that AI clients can talk to).

## Connect an AI client

Point your AI client at the running server:

- **Claude Desktop** — add a server entry pointing at the Unity MCP endpoint
  (see the Unity MCP README for the exact `mcpServers` JSON).
- **Cursor / Windsurf** — same idea; configure the MCP server in the client's
  MCP settings.

Once connected, the client can query, create, and modify scenes, GameObjects,
scripts, and assets directly in this project.

## Official resources

- GitHub: <https://github.com/Unity-Technologies/unity-mcp>
- Package name: `com.unity.ai.mcp`
- Install guide: <https://docs.beam.game/ai/mcp/unity-mcp-server-package-installation-guide>
