namespace Olve.Engine3D;

public static class CardinalDirectionExtensions
{
    private static readonly CardinalDirection[] HorizontalDirections =
    [
        CardinalDirection.North,
        CardinalDirection.South,
        CardinalDirection.East,
        CardinalDirection.West,
    ];

    public static CardinalDirection RotateClockwise(this CardinalDirection direction)
    {
        return direction switch
        {
            CardinalDirection.North => CardinalDirection.East,
            CardinalDirection.South => CardinalDirection.West,
            CardinalDirection.East => CardinalDirection.South,
            CardinalDirection.West => CardinalDirection.North,
            _ => direction
        };
    }

    public static CardinalDirection RotateCounterClockwise(this CardinalDirection direction)
    {
        return direction switch
        {
            CardinalDirection.North => CardinalDirection.West,
            CardinalDirection.South => CardinalDirection.East,
            CardinalDirection.East => CardinalDirection.North,
            CardinalDirection.West => CardinalDirection.South,
            _ => direction
        };
    }

    public static Vector3 ToVector3(this CardinalDirection cardinalDirection)
    {
        return cardinalDirection switch
        {
            CardinalDirection.North => new Vector3(0, 0, 1),
            CardinalDirection.South => new Vector3(0, 0, -1),
            CardinalDirection.East => new Vector3(1, 0, 0),
            CardinalDirection.West => new Vector3(-1, 0, 0),
            CardinalDirection.Up => new Vector3(0, 1, 0),
            CardinalDirection.Down => new Vector3(0, -1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(cardinalDirection), cardinalDirection, null)
        };
    }

    public static CardinalDirection ToCardinalDirection(this Vector3 direction)
    {
        var flat = new Vector3(direction.X, 0, direction.Z);
        if (flat.Length() < Epsilon) return CardinalDirection.None;
        flat = Vector3.Normalize(flat);

        var bestDirection = CardinalDirection.None;
        var bestDot = float.MinValue;
        foreach (var candidate in HorizontalDirections)
        {
            var dot = Vector3.Dot(flat, candidate.ToVector3());
            if (dot > bestDot)
            {
                bestDot = dot;
                bestDirection = candidate;
            }
        }

        return bestDirection;
    }

    public static float ToYRotation(this CardinalDirection cardinalDirection)
    {
        return cardinalDirection switch
        {
            CardinalDirection.North => 0,
            CardinalDirection.South => float.Pi,
            CardinalDirection.East => PiOver2,
            CardinalDirection.West => -PiOver2,
            _ => throw new ArgumentOutOfRangeException(nameof(cardinalDirection), cardinalDirection, null)
        };
    }
}
