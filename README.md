# AndiBanterBot

AndiBanterBot is a Twitch chat bot designed to enhance your Twitch stream with interactive features and engaging chat interactions.

# .NET version

This is the to be version when going forward. Most development will hopefully
be made during streaming at https://www.twitch.tv/littleandi77.

We now got a basic bot that responds when mentioned, but also randomly responds to messages in chat.
Also, we're trying out making it aware of some of the chat history, but this seems to require some tuning.

## Auth

Scopes needed `user:bot chat:edit chat:read channel:bot whispers:read clips:edit`

## Getting Started

To get started with AndiBanterBot Node version, follow these steps:

1. Clone the repository: `git clone https://github.com/LittleAndi/AndiBanterBot.git`
2. Run `dotnet restore` to get all dependencies
3. Start with `dotnet run --project .\src\Host\`

After starting the app, navigate to the start page, i.e. http://localhost:5000/, and click the Login link.
This will start the OAuth flow and take you to https://id.twitch.tv/oauth2/authorize. After completing
the login, the app will get a access token back. This access token will then be used when using the Twitch
APIs later.

# Contributing

Contributions are welcome! If you have any suggestions, bug reports, or feature requests, please open an issue on the GitHub repository.

# License

This project is licensed under the [MIT License](LICENSE).
