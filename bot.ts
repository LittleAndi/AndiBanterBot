import { ChatUserstate, Userstate, client as _client } from "tmi.js";
import OpenAI from "openai";
import { ChatCompletionCreateParamsNonStreaming } from "openai/resources/chat/completions";

const opts = {
  identity: {
    username: process.env.USERNAME,
    password: "oauth:" + process.env.TOKEN,
  },
  channels: ["littleandi77", "nicholasdipples"],
};

const client = new _client(opts);
const openai = new OpenAI();

// Register our event handlers
client.on("message", onMessageHandler);
client.on("whisper", onWhisperHandler);
client.on("connected", onConnectedHandler);

// Connect to Twitch
client.connect();

// Called every time a message comes in
async function onMessageHandler(
  channel: string,
  userstate: ChatUserstate,
  message: string,
  self: boolean
) {
  if (self) {
    return;
  } // Ignore messages from the bot

  // console.log(userstate);
  // console.log(channel);

  if (channel != "#littleandi77" && !isSub(userstate)) {
    return;
  } // Ignore non subs (but always accept everything in channel littleandi77)

  // Remove whitespace from chat message
  var trimmedMsg = message.trim();
  var random = Math.random();

  console.log("%s: %s (%f)", userstate.username, trimmedMsg, random);

  if (isCommand(trimmedMsg)) {
    return;
  } // Ignore commands

  // Bot needs to mentioned or 20% chance of it responding anyway
  if (random > 1 || isMentioned(trimmedMsg)) {
    var response = await makeChatGptRequest(trimmedMsg, userstate);

    client.say(channel, response);
  }
}

// Called every time the bot connects to Twitch chat
function onConnectedHandler(address: string, port: number) {
  console.log(`* Connected to ${address}:${port}`);
}

// Define the request payload
interface ChatGPTRequest {
  model: string;
  messages: { role: string; content: string }[];
}

// Define the response payload
interface ChatGPTResponse {
  choices: { message: { role: string; content: string; name: string } }[];
  usage: { total_tokens: number };
}

// Make a request to the OpenAI API
async function makeChatGptRequest(
  prompt: string,
  chatUserState: ChatUserstate
): Promise<string> {
  try {
    var params: ChatCompletionCreateParamsNonStreaming = {
      model: "gpt-3.5-turbo-0125",
      messages: [
        {
          role: "system",
          content: `Your primary role on this Twitch channel is to facilitate and enhance the interactions between human users.
            While you may receive mentions (@andibanterbot), these should be treated as proactive requests for assistance or information.
            The vast majority of messages in the chat are conversations between users and the streamer.
            Your task is to observe these conversations and respond appropriately, supporting the streamer and the community.
            Avoid actively engaging in conversations unless explicitly addressed.
            Be respectful of all users and refrain from self-promotion.
            Your presence is to complement and support the streamer and the community, not to be the focus of attention.
            You act as a female chatter, and you are intelligent and funny.
            Use short responses, usually max one sentence.
            If someone asks you to join a Stream Racer race, just say "race" or a scentence with "race" in it.`,
        },
        { role: "user", content: prompt, name: chatUserState.username },
      ],
    };

    // If this is reply, take that message into account too
    if (chatUserState["reply-parent-msg-body"]) {
      params.messages.push({
        role: "user",
        content: chatUserState["reply-parent-msg-body"],
        name: chatUserState["reply-parent-display-name"],
      });
    }

    const completion = await openai.chat.completions.create(params);

    // console.log(completion.choices[0]);
    var content = completion.choices[0].message.content ?? "";
    console.log("> " + content + " (%d)", completion.usage?.total_tokens);
    return content;
  } catch (error: any) {
    console.error("Error:", error.message);
    return "";
  }
}

function isEmptyOrNull(str: string | null | undefined): boolean {
  return (str ?? "").trim() === "";
}

function isMentioned(msg: string): boolean {
  return msg.toLowerCase().includes("andibanterbot");
}

function isCommand(msg: string): boolean {
  return false;
}

function isSub(context: any): boolean {
  return context["subscriber"] as boolean;
}

function onWhisperHandler(
  from: string,
  userstate: ChatUserstate,
  message: string,
  self: boolean
): void {
  //throw new Error("Function not implemented.");
  console.log(from);
  console.log(userstate);
  console.log(message);
}

// setTimeout(async () => {
//   var response = await makeJoke("Tell us a joke");
//   client.say("littleandi77", response);
// }, 5000);

async function makeJoke(prompt: string): Promise<string> {
  try {
    var params: ChatCompletionCreateParamsNonStreaming = {
      model: "gpt-3.5-turbo",
      messages: [
        {
          role: "system",
          content: `You are a female intelligent funny chatter on Twitch. You make jokes.`,
        },
        { role: "user", content: prompt },
      ],
    };

    const completion = await openai.chat.completions.create(params);
    var content = completion.choices[0].message.content ?? "";
    return content;
  } catch (error: any) {
    console.error("Error:", error.message);
    return "";
  }
}
