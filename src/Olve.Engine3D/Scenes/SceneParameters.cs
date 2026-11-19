using Olve.Utilities.Ids;

namespace Olve.Engine3D.Scenes;

internal interface ISceneParameters
{
    Result Complete();
}

/// <summary>A scene's parameters: the arguments it was loaded with, else the defaults; without defaults they are required.</summary>
public sealed class SceneParameters<T>(Id<IScene> sceneId, Func<T>? defaults) : ISceneParameters
    where T : class
{
    private T? _value;
    private bool _completed;

    public T Value => _completed
        ? _value!
        : throw new InvalidOperationException(
            $"Parameters of type {typeof(T).Name} for scene '{sceneId}' were read before the scene started loading.");

    internal void Set(T value) => _value = value;

    Result ISceneParameters.Complete()
    {
        _value ??= defaults?.Invoke();
        if (_value is null)
        {
            return new ResultProblem("Scene '{0}' needs parameters of type {1}", sceneId, typeof(T).Name);
        }

        _completed = true;
        return Result.Success();
    }
}
