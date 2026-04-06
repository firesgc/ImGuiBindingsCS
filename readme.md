Tool to create C# Bindings for ImGui

Bindings files:
https://github.com/dearimgui/dear_bindings
DearBindings_v0.18_ImGui_v1.92.7-docking

## Generated output

The generator outputs two subdirectories, each containing a **self-contained** set of
files that can be included in your project without conflicts:

```
generated/
  with_defaults/               ← includes convenience overloads (default arguments)
    imgui_shared.cs
    dcimgui.cs                 ← public API with default-arg convenience overloads
    dcimgui_internal.cs        ← internal API (optional, omit with --no-internal)
  without_defaults/            ← raw API only (no convenience overloads)
    imgui_shared.cs
    dcimgui_nodefaultargfunctions.cs            ← public API, exact C signatures
    dcimgui_nodefaultargfunctions_internal.cs   ← internal API (optional)
```

**Pick one folder and include all `.cs` files from it.** Do not mix files across
the two folders — they share many function signatures and will cause CS0111
(duplicate member) errors.

- **`with_defaults/`** — Recommended for most users. Includes overloads like
  `Begin(name)` that fill in default values for optional parameters.
- **`without_defaults/`** — Use if you want only the raw P/Invoke signatures
  matching the C API exactly (one C# method per C function).

If you don't need internal ImGui APIs, pass `--no-internal` to exclude the
`_internal` files.

