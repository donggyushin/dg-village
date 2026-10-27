using System;
using UnityEngine;

[Serializable]
public class DirectionalSprites
{
    [SerializeField] private Sprite _south;
    [SerializeField] private Sprite _southEast;
    [SerializeField] private Sprite _east;
    [SerializeField] private Sprite _northEast;
    [SerializeField] private Sprite _north;
    [SerializeField] private Sprite _northWest;
    [SerializeField] private Sprite _west;
    [SerializeField] private Sprite _southWest;

    public Sprite Get(Direction direction) => direction switch
    {
        Direction.SouthEast => _southEast,
        Direction.East => _east,
        Direction.NorthEast => _northEast,
        Direction.North => _north,
        Direction.NorthWest => _northWest,
        Direction.West => _west,
        Direction.SouthWest => _southWest,
        _ => _south,
    };

}
