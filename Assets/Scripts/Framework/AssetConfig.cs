using UnityEngine;

public sealed class AssetConfig : MonoSingleton<AssetConfig>
{
    public Texture2D backgroundTexture;
    public Texture2D blackBallTexture;
    public Texture2D whiteBallTexture;
    public Texture2D greenBallTexture;

    public Texture2D GetBallTexture(BallColorType color)
    {
        switch (color)
        {
            case BallColorType.Black: return blackBallTexture;
            case BallColorType.White: return whiteBallTexture;
            case BallColorType.Green: return greenBallTexture;
            default: return null;
        }
    }
}
