namespace Application.Features.Twitch;

public static class SubscriptionTierLabel
{
    public static string For(string tier) => tier switch
    {
        "1000" => "1",
        "2000" => "2",
        "3000" => "3",
        _ => tier
    };
}
