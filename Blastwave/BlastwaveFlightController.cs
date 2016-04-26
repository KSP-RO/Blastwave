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

        void Awake()
        {
            instance = this;
            buildings = new HashSet<DestructibleBuilding>();
            DestructibleBuilding.OnLoaded.Add(AddDestructibleBuilding);
        }

        void Start()
        {
            Blastwave.Setup();
            GameEvents.onPartDestroyed.Add(CreateExplosion);
        }

        void FixedUpdate()
        {
            if (FlightGlobals.ready)
            {
                Blastwave.SimulateActiveBlastwaves();
            }
        }

        void CreateExplosion(Part p)
        {
            if (FlightGlobals.ready)
            {
                double resourceMass = 0;
                for (int i = 0; i < p.Resources.Count; ++i)
                    resourceMass += p.Resources[i].amount * p.Resources[i].info.density;

                float yield = (float)resourceMass * 1000f;      //convert t into kg
                yield *= 46;            //yield in MJ
                yield *= 1000000f;      //convert to joules

                yield *= 0.15f;      //assume only 15% is converted to blast



                if (yield > 0.01f && p.staticPressureAtm > 0)
                {
                    Debug.Log("Explosion created with yield of " + yield + "J");
                    Blastwave.CreateBlastwave(yield, p.partTransform.position);
                }
            }
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
            DestructibleBuilding.OnLoaded.Remove(AddDestructibleBuilding);
        }
    }
}
