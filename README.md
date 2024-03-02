# AndiBanterBot

AndiBanterBot is a Twitch chat bot designed to enhance your Twitch stream with interactive features and engaging chat interactions.

## .NET version

This is the to be version when going forward. Most development will hopefully
be made during streaming at https://www.twitch.tv/littleandi77.

For now it is just an empty project.

More will follow here...

## node version

The node version was the first version when exploring this technology combining
Twitch Chat with Open AI ChatGPT models. This is no longer maintained and all
new additions are made in the .NET version instead.

### Features

- **Chat-based interactions**: AndiBanterBot responds to chat messages using Open AI APIs to generate the response.
- **Customizable responses**: Configure AndiBanterBot to respond to specific keywords or phrases in the chat, adding a personal touch to your stream.

#### Getting Started

To get started with AndiBanterBot, follow these steps:

1. Clone the repository: `git clone https://github.com/LittleAndi/AndiBanterBot.git`
2. Switch to `src_node` folder
3. Install the required dependencies: `npm install`
4. Configure the bot by creating `.env` file and adding your Twitch API and ChatGTP credentials
5. Run the bot: `npm run dev`

#### Configuration

AndiBanterBot is configured by creating the `.env` file. Here are some of the key configuration options:

- `USERNAME`: Your Twitch username.
- `TOKEN`: Your Twitch OAuth token.
- `OPENAI_API_KEY`: Your Open AI API key.

## Contributing

Contributions are welcome! If you have any suggestions, bug reports, or feature requests, please open an issue on the GitHub repository.

## License

This project is licensed under the [MIT License](LICENSE).
