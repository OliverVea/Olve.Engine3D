using Olve.Utilities.Ids;

namespace Olve.Engine3D.Scenes;

/// <summary>
/// The scene <see cref="GameManager"/> loads and activates when the game starts. The game registers it.
/// </summary>
public sealed record StartScene(Id<IScene> SceneId);
