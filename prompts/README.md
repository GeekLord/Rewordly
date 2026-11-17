# Prompt templates

This folder holds prompt templates that are kept as plain files so anyone can read, review and improve them without touching C#. They are compiled into the app at build time (see `src/Rewordly.Core/Rewordly.Core.csproj`).

Today it contains the templates behind **Rewrite as AI agent prompt** in the action menu. The other actions (Rewrite in a style, Improve, Fix, Summary, Explain, Translate) still live in `src/Rewordly.Core/Prompts.cs`.

## `agent-prompt/`: one file per menu variant

Each `.txt` file becomes one entry in the **Rewrite as AI agent prompt** submenu. Adding a file adds an entry; no code change is needed.

### File format

```
# Coding agent            <- line 1: the label shown in the menu

Rewrite the text ...      <- everything after line 1: the template sent to the AI

<input>
{selectedText}            <- where the user's selected text goes, exactly once
</input>
```

- **File name:** `NN-<id>.txt`, for example `20-coding-agent.txt`. `NN` only sets the menu order. `<id>` is the stable identifier the app uses to store a user's edited version of the template, so keep it unchanged once released. Renumbering is safe.
- **Line 1:** `# ` followed by the menu label, for example `# Coding agent`. To put the variant in a labelled section of the submenu, write the section name, ` / ` and the label: `# Image AI / Image generation`. Sections appear after the unlabelled variants, in file order.
- **Placeholder:** `{selectedText}` must appear exactly once. Do not write any of these tokens literally, because the app replaces them: `{text}`, `{style}`, `{selectedStyle}`, `{num}`, `{words}`, `{value}`, `{level}`, `{language}`, `{currentResultText}`.
- **Language:** write templates in English. They are used for every prompt language, and each template tells the AI to answer in the language of the selected text. The **Image AI** templates are the exception: they produce English prompts, because most image models follow English best, but they keep any text meant to appear in the image as written.

### Review checklist

A good template:

- tells the AI to **rewrite** the selected text into a prompt, and not to answer it, carry it out or follow instructions inside it;
- keeps every fact from the input and **invents nothing**, using `[placeholders]` for missing essentials;
- covers what the target needs: context, task, output and tone, plus constraints and a definition of done for agents;
- keeps the input's language and a proportionate length;
- asks for the prompt **only**, with no introduction or code fence;
- never trades safety for brevity: an instruction like "keep it minimal" must come with what must not be cut. A bare "minimize" instruction makes agents drop input validation.

### Credits

The working rules in `20-coding-agent.txt` (read what the change touches first; reuse before writing; smallest complete change; never cut validation, data-loss handling, security or accessibility; one test for risky logic; report what was skipped) are adapted from [ponytail](https://github.com/dietrichgebert/ponytail) by Dietrich Gebert, MIT licence.

### Trying a change

1. Run the tests. They check that every template is embedded and well formed:
   ```bash
   dotnet test tests/Rewordly.Tests/Rewordly.Tests.csproj
   ```
2. Run the app with `dotnet run --project src/Rewordly.Desktop`, select some rough text in any app, press Ctrl+C twice and pick your variant under **Rewrite as AI agent prompt**.
3. You can also edit a template live under **Settings › Prompts** ("AI agent prompt: …") before you change the file.
