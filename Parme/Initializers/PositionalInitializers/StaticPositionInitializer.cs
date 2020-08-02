﻿using Microsoft.Xna.Framework;

namespace Parme.Initializers.PositionalInitializers
{
    public class StaticPositionInitializer : IPositionalInitializer
    {
        private readonly Vector2 _position;

        public StaticPositionInitializer(Vector2 position)
        {
            _position = position;
        }

        public Vector2 GetNewParticlePosition()
        {
            return _position;
        }
    }
}