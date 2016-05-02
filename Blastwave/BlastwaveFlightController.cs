using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Blastwave
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class BlastwaveFlightController : MonoBehaviour
    {
        static BlastwaveFlightController instance;
        public static BlastwaveFlightController Instance
        {
            get { return instance; }
        }

        public HashSet<DestructibleBuilding> buildings;
        public ExplosiveReactions explosiveHandler;

        void Awake()
        {
            instance = this;
            buildings = new HashSet<DestructibleBuilding>();
            DestructibleBuilding.OnLoaded.Add(AddDestructibleBuilding);
        }

        void Start()
        {
            Blastwave.Setup();

            explosiveHandler = new ExplosiveReactions();
            GameEvents.onPartDestroyed.Add(CreateExplosion);
            GameEvents.onPartUnpack.Add(SetExplosionPotential);
        }

        void FixedUpdate()
        {
            if (FlightGlobals.ready)
            {
                explosiveHandler.UpdateFreeReactantsAndReactions();
                Blastwave.SimulateActiveBlastwaves();
            }
        }

        void CreateExplosion(Part p)
        {
            if (FlightGlobals.ready)
            {
                explosiveHandler.CreateFreeReactantsFromPart(p);
            }
        }

        void SetExplosionPotential(Part p)
        {
            p.explosionPotential = 0;
        }

        void AddDestructibleBuilding(DestructibleBuilding b)
        {
            if (b != null || buildings.Contains(b))
                buildings.Add(b);

            buildings.RemoveWhere(x => x == null);
        }

        void OnDestroy()
        {
            GameEvents.onPartDestroyed.Remove(CreateExplosion);
            GameEvents.onPartUnpack.Remove(SetExplosionPotential);

            DestructibleBuilding.OnLoaded.Remove(AddDestructibleBuilding);
        }
    }
}
