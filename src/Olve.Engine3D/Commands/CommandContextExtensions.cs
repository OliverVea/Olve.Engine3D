using System.Globalization;
using Olve.Engine3D.Time;
using Olve.Utilities.Ids;

namespace Olve.Engine3D.Commands;

public static class CommandContextExtensions
{
    public static Result<string> GetRequiredArgument(this CommandContext commandContext, CommandArgument commandArgument)
        => commandContext.GetArgument(commandArgument) is {} value
            ? value
            : new ResultProblem("Required argument '{0}' not found", commandArgument.Key);

    public static string? GetOptionalArgument(this CommandContext commandContext, CommandArgument commandArgument)
        => commandContext.GetArgument(commandArgument);

    public static Result<bool> GetFlag(this CommandContext commandContext, CommandArgument commandArgument)
    {
        var text = commandContext.GetOptionalArgument(commandArgument) ?? "false";
        return bool.TryParse(text, out var value)
            ? value
            : new ResultProblem("'{0}' isn't true or false for {1}", text, commandArgument.Name);
    }

    public static Result<Id<T>> GetId<T>(this CommandContext commandContext, CommandArgument commandArgument)
        => Id.TryParse<T>(commandContext.Arguments[commandArgument.Key], out var id) ? id : new ResultProblem("Could not parse id '{0}' as Id", commandContext.Arguments[commandArgument.Key]);

    public static Result<Vector3> ParseVector3(this string input)
    {
        var parts = input.Split(',');
        if (parts.Length != 3)
        {
            return new ResultProblem("Expected 3 comma-separated values (x,y,z), got '{0}'", input);
        }

        if (!float.TryParse(parts[0].Trim(), NumberFormatInfo.InvariantInfo, out var x)
            || !float.TryParse(parts[1].Trim(), NumberFormatInfo.InvariantInfo, out var y)
            || !float.TryParse(parts[2].Trim(), NumberFormatInfo.InvariantInfo, out var z))
        {
            return new ResultProblem("Could not parse coordinates from '{0}'", input);
        }

        return new Vector3(x, y, z);
    }

    public static Result<Vector2> ParseVector2(this string input)
    {
        var parts = input.Split(',');
        if (parts.Length != 2)
        {
            return new ResultProblem("Expected 2 comma-separated values (x,y), got '{0}'", input);
        }

        if (!float.TryParse(parts[0].Trim(), NumberFormatInfo.InvariantInfo, out var x)
            || !float.TryParse(parts[1].Trim(), NumberFormatInfo.InvariantInfo, out var y))
        {
            return new ResultProblem("Could not parse coordinates from '{0}'", input);
        }

        return new Vector2(x, y);
    }

    public static Result<CardinalDirection> ParseDirection(this string input)
    {
        var direction = input.ToLowerInvariant() switch
        {
            "north" or "n" => CardinalDirection.North,
            "south" or "s" => CardinalDirection.South,
            "east" or "e" => CardinalDirection.East,
            "west" or "w" => CardinalDirection.West,
            _ => CardinalDirection.None
        };

        if (direction == CardinalDirection.None)
        {
            return new ResultProblem("Invalid direction '{0}'. Use: north, south, east, west (or n, s, e, w)", input);
        }

        return direction;
    }

    public static Result<TilePosition> ParseTilePosition(this string input)
    {
        var parts = input.Split(',');

        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0].Trim(), NumberFormatInfo.InvariantInfo, out var x)
                || !int.TryParse(parts[1].Trim(), NumberFormatInfo.InvariantInfo, out var z))
            {
                return new ResultProblem("Could not parse tile position from '{0}'", input);
            }

            return new TilePosition(x, 0, z);
        }

        if (parts.Length == 3)
        {
            if (!int.TryParse(parts[0].Trim(), NumberFormatInfo.InvariantInfo, out var x)
                || !int.TryParse(parts[1].Trim(), NumberFormatInfo.InvariantInfo, out var y)
                || !int.TryParse(parts[2].Trim(), NumberFormatInfo.InvariantInfo, out var z))
            {
                return new ResultProblem("Could not parse tile position from '{0}'", input);
            }

            return new TilePosition(x, y, z);
        }

        return new ResultProblem("Expected 2 or 3 comma-separated values (x,z or x,y,z), got '{0}'", input);
    }

    public static Result<DayTime> ParseDayTime(this string input)
    {
        var parts = input.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberFormatInfo.InvariantInfo, out var hour)
            || !int.TryParse(parts[1], NumberFormatInfo.InvariantInfo, out var minute))
        {
            return new ResultProblem("Could not parse day time from '{0}'", input);
        }

        return new DayTime(hour, minute);
    }
}
