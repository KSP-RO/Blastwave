using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Blastwave
{
    class ExplosiveReactions
    {
        class ExplosiveReaction
        {
            public Dictionary<int, double> reactantRatios = new Dictionary<int,double>();
            public FloatCurve PYROYieldCurve;
            public bool requiresOxygenAtm = false;
            public float maxOverPreskPa = float.PositiveInfinity;
            public float impulseFactor = 1;
        }

        Dictionary<int, HashSet<ExplosiveReaction>> reactantPotentialReactions;
        List<FreeReactant> freeReactants;

        public ExplosiveReactions()
        {
            Debug.Log("[Blastwave]: Creating Explosive Reaction Database");
            freeReactants = new List<FreeReactant>();
            reactantPotentialReactions = new Dictionary<int, HashSet<ExplosiveReaction>>();

            LoadConfigs();
        }

        public void CreateFreeReactantsFromPart(Part p)
        {
            for(int i = 0; i < p.Resources.Count; ++i)
            {
                PartResource res = p.Resources[i];
                if (reactantPotentialReactions.ContainsKey(res.info.id))       //check to see if this resource can react
                {
                    freeReactants.Add(new FreeReactant(res, p.partTransform.position, p.vel));
                }
            }
        }

        #region FixedUpdate Reactant Handling

        public void UpdateFreeReactantsAndReactions()
        {
            //check for reaction potential
            //
            //
            RunAllReactions();


            for(int i = freeReactants.Count - 1; i >= 0; --i)
            {
                freeReactants[i].FixedUpdate(TimeWarp.fixedDeltaTime);
                if (freeReactants[i].expired)
                {
                    freeReactants.RemoveAt(i);
                }
            }
        }

        void RunAllReactions()
        {
            for(int i = 0; i < freeReactants.Count; ++i)
            {
                FreeReactant reactantI = freeReactants[i];
                Vector3d reactantIPos = reactantI.worldPosition;
                double reactantIRad = reactantI.effectiveRadius;

                if (reactantI.resourceMass <= 0)
                    continue;

                for (int j = i + 1; j < freeReactants.Count; ++j)
                {
                    if (i == j)
                        continue;
                    FreeReactant reactantJ = freeReactants[j];

                    if (reactantI.resource.id == reactantJ.resource.id)
                        continue;

                    if (reactantJ.resourceMass <= 0)
                        continue;

                    if ((reactantIPos - reactantJ.worldPosition).magnitude > (reactantIRad + reactantJ.effectiveRadius) * 0.9)
                        continue;

                    HashSet<ExplosiveReaction> iReactionSet = reactantPotentialReactions[reactantI.resource.id];
                    HashSet<ExplosiveReaction> jReactionSet = reactantPotentialReactions[reactantJ.resource.id];

                    ExplosiveReaction thisReaction = null;
                    if (iReactionSet.Count > jReactionSet.Count)
                    {
                        foreach (ExplosiveReaction e in jReactionSet)
                            if (iReactionSet.Contains(e))
                            {
                                thisReaction = e;
                                break;
                            }
                    }
                    else
                    {
                        foreach (ExplosiveReaction e in iReactionSet)
                            if (jReactionSet.Contains(e))
                            {
                                thisReaction = e;
                                break;
                            }
                    }

                    if (thisReaction != null)
                        CreateExplosion(thisReaction, reactantI, reactantJ);
                }
            }
        }

        void CreateExplosion(ExplosiveReaction reaction, FreeReactant reactant1, FreeReactant reactant2)
        {
            double reactantRatio = reaction.reactantRatios[reactant1.resource.id] / reaction.reactantRatios[reactant2.resource.id];

            double reactant1Amount = reactant1.resourceMass;
            double reactant2Amount = reactant2.resourceMass;

            double fuelOxRatio = reactant2Amount * reactantRatio / reactant1Amount;

            if(fuelOxRatio > 1)     //means there is more reactant 2 than is needed
            {
                reactant2Amount = reactant1Amount / reactantRatio;      //amount of reactant2 needed for reaction

                reactant1.resourceMass -= reactant1Amount;          //remove this from FreeReactant
                reactant2.resourceMass -= reactant2Amount;
            }
            else
            {
                reactant1Amount = reactant2Amount * reactantRatio;      //amount of reactant2 needed for reaction

                reactant1.resourceMass -= reactant1Amount;          //remove this from FreeReactant
                reactant2.resourceMass -= reactant2Amount;
            }

            Vector3d avgVel = reactant1.worldVelocity * reactant1Amount + reactant2.worldVelocity * reactant2Amount;
            avgVel /= reactant1Amount + reactant2Amount;

            double fractionTNTEquivalent = reaction.PYROYieldCurve.Evaluate((float)avgVel.magnitude) * 4.184e6 * (reactant1Amount + reactant2Amount);

            Vector3d avgPos = reactant1.worldPosition * reactant1Amount + reactant2.worldPosition * reactant2Amount;
            avgPos /= reactant1Amount + reactant2Amount;


            Debug.Log("[Blastwave]: Create Explosion with yield of " + fractionTNTEquivalent + " J");

            //create blastwave
            Blastwave.CreateBlastwave((float)fractionTNTEquivalent, reaction.maxOverPreskPa, reaction.impulseFactor, avgPos);

            //create fireball
            //TODO fireball call
        }
        #endregion

        #region Loading Reaction Configs

        void LoadConfigs()
        {
            ConfigNode[] allReactionConfigNodes = GameDatabase.Instance.GetConfigNodes("BlastwaveResourceReactions");

            //First, get all the reactants so that we can properly place the reactions
            for(int i = 0; i < allReactionConfigNodes.Length; ++i)
            {
                ConfigNode node = allReactionConfigNodes[i];
                if (node == null || !node.HasNode("ReactiveResources"))
                    continue;

                ConfigNode reactantNode = node.GetNode("ReactiveResources");

                AddResourcesToPotentialReactions(reactantNode);
            }

            //then, build up all the reactions as needed
            for (int i = 0; i < allReactionConfigNodes.Length; ++i)
            {
                ConfigNode node = allReactionConfigNodes[i];
                if (node == null || !node.HasNode("ExplosiveReaction"))
                    continue;

                ConfigNode[] explosiveNodes = node.GetNodes("ExplosiveReaction");

                for (int j = 0; j < explosiveNodes.Length; ++j)
                    AddExplosiveReactionToDict(explosiveNodes[j]);
            }

        }

        void AddResourcesToPotentialReactions(ConfigNode reactantNode)
        {
            if (reactantNode == null)
                return;

            PartResourceDefinitionList resourceDefinitions = PartResourceLibrary.Instance.resourceDefinitions;

            for (int j = 0; j < reactantNode.values.Count; ++j)
            {
                ConfigNode.Value v = reactantNode.values[j];
                if (v.name != "resource")
                    continue;

                string resString = v.value;
                if (resourceDefinitions.Contains(resString))
                {
                    PartResourceDefinition p = resourceDefinitions[resString];

                    if (reactantPotentialReactions.ContainsKey(p.id))
                        continue;

                    Debug.Log("[Blastwave]: Adding " + p.name + " as possible reactant");
                    reactantPotentialReactions.Add(p.id, new HashSet<ExplosiveReaction>());
                }
            }
        }

        void AddExplosiveReactionToDict(ConfigNode explosiveNode)
        {
            if (explosiveNode == null)
                return;

            PartResourceDefinitionList resourceDefinitions = PartResourceLibrary.Instance.resourceDefinitions;

            ExplosiveReaction reaction = new ExplosiveReaction();

            if (explosiveNode.HasValue("requiresOxygenAtm"))
                bool.TryParse(explosiveNode.GetValue("requiresOxygenAtm"), out reaction.requiresOxygenAtm);

            if (explosiveNode.HasValue("maxOverPreskPa"))
                float.TryParse(explosiveNode.GetValue("maxOverPreskPa"), out reaction.maxOverPreskPa);

            if (explosiveNode.HasValue("impulseFactor"))
                float.TryParse(explosiveNode.GetValue("impulseFactor"), out reaction.impulseFactor);

            if(explosiveNode.HasNode("PYROYieldCurve"))
            {
                reaction.PYROYieldCurve = new FloatCurve();
                reaction.PYROYieldCurve.Load(explosiveNode.GetNode("PYROYieldCurve"));
            }

            if(explosiveNode.HasValue("resource"))
            {
                string[] vals = explosiveNode.GetValues("resource");
                for(int j = 0; j < vals.Length; ++j)
                {
                    string[] splitStr = vals[j].Split(new char[] { ',', ' ' });

                    PartResourceDefinition res = null;
                    double resRatio = 0;

                    for(int i = 0; i < splitStr.Length; ++i)
                    {
                        if (splitStr[i].Length <= 0)
                            continue;

                        if(res == null && resourceDefinitions.Contains(splitStr[i]))
                        {
                            res = resourceDefinitions[splitStr[i]];
                            continue;
                        }
                        else
                        {
                            if (double.TryParse(splitStr[i], out resRatio))
                                break;
                        }
                    }

                    if(res == null && resRatio <= 0)
                    {
                        Debug.LogError("[Blastwave]: Couldn't find resource for reaction");
                        continue;
                    }

                    reaction.reactantRatios.Add(res.id, resRatio);
                    reactantPotentialReactions[res.id].Add(reaction);
                }
            }
        }
        #endregion

    }
}
