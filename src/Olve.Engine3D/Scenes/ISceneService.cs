namespace Olve.Engine3D.Scenes;

public interface ISceneService
{
    int Priority => 0;
    Result Load() => Result.Success();
    Result Unload() => Result.Success();
    Result Input() => Result.Success();
    Result Update() => Result.Success();
    Result Render() => Result.Success();
}
