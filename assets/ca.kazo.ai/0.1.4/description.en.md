# Kazo AI

A chat with AI models for Kazo OS, written in C# and Blazor, on a computer and on a phone. Bring
your own API keys, or run a model directly in your browser: nothing to install, no account needed.

## Your models, your keys

- **OpenRouter, OpenAI, Ollama, LM Studio**, and any service that speaks the OpenAI chat API: add
  its address, paste your key, list its models and pick one.
- Keys stay in the **Kazo OS keyring** and are sent only to their own service, straight from the
  browser. They are never written to a file and never exported.
- Answers are streamed as they are written, in Markdown, with code blocks and tables.

## A local model, on your graphics card

- No key? Kazo AI runs a small open model **in the browser with WebGPU** (WebLLM): Qwen, Llama,
  Phi, Gemma and others. The model is downloaded once and kept in the browser cache.
- What you write to the local model never leaves your computer.
- It loads by itself at start when already downloaded, and takes over when the chosen service has
  no key.

## Race and AutoTune

- **Race mode** asks several models at once, scores their answers as they arrive, shows the
  leader live, and keeps the best one, with the full ranking.
- **AutoTune** reads the conversation (code, writing, analysis, small talk...) and sets the
  temperature and the other sampling parameters for each answer. Rate answers with 👍 and 👎 and it
  learns your taste; or choose a fixed strategy, or set everything by hand.

## Personas and modules

- Personas (assistant, developer, writer, teacher, or your own) give the system prompt of a
  conversation.
- **Modules** change the request before it leaves or the answer before it is shown: a few are
  built in, more can be imported as simple JSON files, and developers can compile their own C#
  modules into the app.
- Settings, personas and modules are exported and imported as one JSON file.

## Administrator approval

Kazo AI asks for access to the **real network** (`network.real.access`) to reach the AI services
and to download the local model. Kazo OS asks an administrator to approve it at the first launch.
