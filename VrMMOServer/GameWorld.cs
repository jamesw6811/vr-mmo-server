using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VrMMOServer
{
    class GameWorld
    {
        private Dictionary<UInt32, GameEntity> entities;
        private UInt32 nextID;

        public GameWorld()
        {
            nextID = 0;
            entities = new Dictionary<uint, GameEntity>();
        }

        public GameEntity addEntity(GameEntity ge)
        {
            ge.id = nextID++;
            entities.Add(ge.id, ge);
            return ge;
        }

        public void removeEntity(GameEntity ge)
        {
            entities.Remove(ge.id);
        }

        public void updateEntity(EntityUpdatePacket up)
        {
            GameEntity tobeUpdated;
            if (entities.TryGetValue(up.id, out tobeUpdated))
            {
                up.update(tobeUpdated);
            }
            else
            {
                Console.WriteLine("Tried to update entry, but none found.");
            }
        }

        public ICollection<GameEntity> getAllEntities()
        {
            return entities.Values;
        }
    }
}
