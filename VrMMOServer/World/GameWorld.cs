using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VrMMOServer
{
    public class GameWorld
    {
        private Dictionary<UInt32, GameEntity> entities;
        private UInt32 nextID;
        private List<GameEntity> removedEntities; // List of all entities removed since the last time pullRemovedEntities() was called
        private List<AIBehavior> AIEntities;
        private Queue<UInt32> entitiesToRemove;

        public static float getDirection(float x1, float y1, float x2, float y2)
        {
            float x = x2 - x1;
            float y = y2 - y1;
            float direction = (float)Math.Atan2(y, x);
            return direction;
        }

        public static float getDistanceSquared(float x1, float y1, float x2, float y2)
        {
            float x = x2 - x1;
            float y = y2 - y1;
            return x * x + y * y;
        }

        public GameWorld()
        {
            nextID = 0;
            entities = new Dictionary<uint, GameEntity>();
            removedEntities = new List<GameEntity>();
            AIEntities = new List<AIBehavior>();
            entitiesToRemove = new Queue<UInt32>();
        }

        public GameEntity addEntity(GameEntity ge)
        {
            ge.id = nextID++;
            entities.Add(ge.id, ge);
            if (ge is OnlinePlayerEntity)
            {
                addPetEntityForPlayer((OnlinePlayerEntity)ge);
            }
            if (ge is AIBehavior)
            {
                AIEntities.Add((AIBehavior)ge);
            }
            return ge;
        }

        public void doGameWorldTick()
        {
            doAllAIBehaviors();
            executeWorldDeletions();
        }

        private void doAllAIBehaviors()
        {
            foreach (AIBehavior ab in AIEntities)
            {
                ab.doBehavior(this);
            }
        }

        private void executeWorldDeletions()
        {
            while(entitiesToRemove.Count > 0)
            {
                removeEntity(entitiesToRemove.Dequeue());
            }
        }

        private void addPetEntityForPlayer(OnlinePlayerEntity ge)
        {
            OnlinePetEntity ope = new OnlinePetEntity(ge.id);
            ope.x = ge.x + 1f;
            ope.y = ge.y + 1f;
            addEntity(ope);
            ge.attachPet(ope.id);
        }
        private void removePetEntityForPlayer(OnlinePlayerEntity ge)
        {
            removeEntity(ge.getAttachedPet());
        }

        public GameEntity getEntity(UInt32 id)
        {
            GameEntity found;
            entities.TryGetValue(id, out found);
            return found;
        }

        /// <summary>
        /// Only to be called by the server, NOT during tick updates.
        /// </summary>
        /// <param name="id"></param>
        public void serverRemoveEntity(UInt32 id)
        {
            removeEntity(id);
        }

        /// <summary>
        /// Only to be called by the world objects, not the server. Removes entity at the end of the tick.
        /// </summary>
        /// <param name="id"></param>
        public void worldRemoveEntity(UInt32 id)
        {
            entitiesToRemove.Enqueue(id);
        }

        private void removeEntity(UInt32 id)
        {
            GameEntity ge = getEntity(id);
            entities.Remove(id);
            removedEntities.Add(ge);

            if (ge is OnlinePlayerEntity)
            {
                removePetEntityForPlayer((OnlinePlayerEntity)ge);
            }
            if (ge is AIBehavior)
            {
                AIEntities.Remove((AIBehavior)ge);
            }
        }

        public void moveEntity(UInt32 id, float x, float y)
        {
            GameEntity ge = getEntity(id);
            ge.x = x;
            ge.y = y;
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
