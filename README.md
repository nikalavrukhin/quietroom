# QuietRoom: an LLM-driven interrogation game (Unity)

First-person detective game. You are an outlaw private investigator holding a suspect tied to a chair in your basement. You must extract a secret code before morning. **The suspect is an LLM**: you talk to him in free text, and he reacts to what you say and do.

## How it works
- **Free-text dialogue with a persistent character.** `Suspect.cs` / `Interrogation.cs` build the prompt from the case file, the suspect's hidden state (health, blood sugar, stress) and the items currently shown or used; `LlmClient.cs` calls any OpenAI-compatible API (OpenRouter / DeepSeek) with a fallback endpoint.
- **Hidden simulation** the player can only read from behaviour (breathing, trembling, voice weakening) and that feeds back into the prompt.
- **Items as game mechanics:** water, chocolate, lamp in the face, photo, folder, gun. Each suspect has his own weakness.
- **Win/lose logic:** verify the code through a friend at a bank on the in-game phone (also an LLM contact); three wrong codes and he refuses to help; the suspect can die; morning comes.
- **The whole scene is built from code**: `RoomBuilder` / `GameBuilder` (Editor) generate the room, props and the macOS build, so the project is reproducible without binary scene files.
- Cases are separate "levels"; the first one is *The Accountant*.

## Run
Unity 6000.6, built-in render pipeline. Copy `llm.example.json` to `llm.json` (never committed) and add an API key, then use the **Подвал** menu: *Build scene* → Play.

Code comments are in Russian. Stack: C#, Unity, LLM prompting, procedural scene generation.
