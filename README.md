# AndiBanterBot

AndiBanterBot is a Twitch chat bot designed to enhance your Twitch stream with interactive features and engaging chat interactions.

## .NET version

This is the to be version when going forward. Most development will hopefully
be made during streaming at https://www.twitch.tv/littleandi77.

We now got a basic bot that responds when mentioned, but also randomly responds to messages in chat.
Also, we're trying out making it aware of some of the chat history, but this seems to require some tuning.

### Getting Started

To get started with AndiBanterBot Node version, follow these steps:

1. Clone the repository: `git clone https://github.com/LittleAndi/AndiBanterBot.git`
2. Run `dotnet restore` to get all dependencies
3. Start with `dotnet run --project .\src\Host\`

After starting the app, navigate to the start page, i.e. http://localhost:5000/, and click the Login link.
This will start the OAuth flow and take you to https://id.twitch.tv/oauth2/authorize. After completing
the login, the app will get a access token back. This access token will then be used when using the Twitch
APIs later.

## node version

The node version was the first version when exploring this technology combining
Twitch Chat with Open AI ChatGPT models. This is no longer maintained and all
new additions are made in the .NET version instead.

### Features

- **Chat-based interactions**: AndiBanterBot responds to chat messages using Open AI APIs to generate the response.
- **Customizable responses**: Configure AndiBanterBot to respond to specific keywords or phrases in the chat, adding a personal touch to your stream.

### Getting Started

To get started with AndiBanterBot Node version, follow these steps:

1. Clone the repository: `git clone https://github.com/LittleAndi/AndiBanterBot.git`
2. Switch to `src_node` folder
3. Install the required dependencies: `npm install`
4. Configure the bot by creating `.env` file and adding your Twitch API and ChatGTP credentials
5. Run the bot: `npm run dev`

#### Auth

I'm using the [Twitch CLI](https://github.com/twitchdev/twitch-cli) to create a User Access Token for Twitch.
I'm using the following scopes `chat:edit chat:read user:bot channel:bot whispers:read`.
A Twitch CLI command for this could look like
`twitch token --client-id <Client ID> -s 'user:bot chat:edit chat:read channel:bot whispers:read' -u`

`Client ID` is fetched from the [Developer Applications](https://dev.twitch.tv/console/apps) in Twitch Dev Console.

#### Configuration

AndiBanterBot is configured by creating the `.env` file. Here are some of the key configuration options:

- `USERNAME`: Your Twitch username.
- `TOKEN`: Your Twitch OAuth token.
- `OPENAI_API_KEY`: Your Open AI API key.

## Contributing

Contributions are welcome! If you have any suggestions, bug reports, or feature requests, please open an issue on the GitHub repository.

## License

This project is licensed under the [MIT License](LICENSE).
