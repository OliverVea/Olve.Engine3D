namespace Olve.Engine3D.Commands;

/// <summary>
/// Feeds commands from outside the game into the <see cref="CommandQueue"/>, such as the desktop host's command pipe.
/// The host registers its sources; <see cref="GameManager"/> starts them once the game has loaded and disposes them
/// when it closes.
/// </summary>
public interface ICommandSource : IDisposable
{
    void Start();
}
