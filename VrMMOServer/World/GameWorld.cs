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
        private List<GameEntity> removedEntities; // List of all entities removed since the last time pullRemovedEntities() was called

        public GameWorld()
        {
            nextID = 0;
            entities = new Dictionary<uint, GameEntity>();
            removedEntities = new List<GameEntity>();
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
            removedEntities.Add(ge);
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

        /// <summary>
        /// Return all removed entities since the last time this method was called.
        /// </summary>
        /// <returns></returns>
        public ICollection<GameEntity> pullRemovedEntities()
        {
            List<GameEntity> removed = removedEntities;
            removedEntities = new List<GameEntity>();
            return removed;
        }
    }
    
}
