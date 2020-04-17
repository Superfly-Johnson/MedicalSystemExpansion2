﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MSE2
{
    public partial class CompIncludedChildParts : ThingComp
    {
        public override void PostSpawnSetup ( bool respawningAfterLoad )
        {
            base.PostSpawnSetup( respawningAfterLoad );
            command_AddExistingSubpart = new Command_AddExistingSubpart( this );
            command_SplitOffSubpart = new Command_SplitOffSubpart( this );
        }

        public CompProperties_IncludedChildParts Props
        {
            get
            {
                return (CompProperties_IncludedChildParts)this.props;
            }
        }

        public List<Thing> childPartsIncluded = new List<Thing>();

        // Creation / Deletion

        public override void PostPostMake ()
        {
            base.PostPostMake();

            // add standard children
            // (if you want it inclomplete replace the list after creating the thing)
            if ( this.Props.standardChildren != null )
            {
                foreach ( ThingDef sChild in this.Props.standardChildren )
                {
                    this.childPartsIncluded.Add( ThingMaker.MakeThing( sChild ) );
                }
            }
        }

        public override void PostDestroy ( DestroyMode mode, Map previousMap )
        {
            base.PostDestroy( mode, previousMap );

            // destroy included child items (idk if it does anything as they aren't spawned)
            if ( this.childPartsIncluded != null )
            {
                foreach ( ThingWithComps childPart in this.childPartsIncluded )
                {
                    childPart.Destroy( DestroyMode.Vanish );
                }
            }
        }

        // Stats display

        public override string CompInspectStringExtra ()
        {
            if ( this.childPartsIncluded != null )
            {
                return "Includes "
                    + this.childPartsIncluded.Count + (this.childPartsIncluded.Count != 1 ? " subparts" : " subpart")
                    + (this.MissingParts.Count() > 0 ? " (incomplete)" : "")
                    + ".";
            }
            return null;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats ()
        {
            if ( this.childPartsIncluded != null )
            {
                var includedPartLinks = new List<Dialog_InfoCard.Hyperlink>(
                    from x in this.childPartsIncluded
                    select new Dialog_InfoCard.Hyperlink( x ) );

                var missingPartLinks = new List<Dialog_InfoCard.Hyperlink>(
                    from x in this.MissingParts
                    select new Dialog_InfoCard.Hyperlink( x ) );

                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "Included subparts:", // Translate
                    includedPartLinks.Count.ToString(),
                    "When implanted it will also install theese:",
                    2500,
                    null,
                    includedPartLinks,
                    false );

                if ( missingPartLinks.Count > 0 )
                {
                    yield return new StatDrawEntry(
                        StatCategoryDefOf.Basics,
                        "Missing subparts:", // Translate
                        missingPartLinks.Count.ToString(),
                        "These parts are missing:",
                        2500,
                        null,
                        missingPartLinks,
                        false );
                }
            }
            yield break;
        }

        // merging

        private Command_AddExistingSubpart command_AddExistingSubpart;
        private Command_SplitOffSubpart command_SplitOffSubpart;

        public override IEnumerable<Gizmo> CompGetGizmosExtra ()
        {
            yield return command_AddExistingSubpart;
            yield return command_SplitOffSubpart;

            foreach ( var g in base.CompGetGizmosExtra() ) yield return g;

            yield break;
        }

        public void AddPart ( Thing part )
        {
            if ( !Props.standardChildren.Contains( part.def ) )
            {
                Log.Warning( part.Label + " is not a valid subpart for " + this.parent.Label );
            }

            this.childPartsIncluded.Add( part );
            this.DirtyCache();

            if ( part.Spawned )
            {
                part.DeSpawn();
            }
        }

        public void RemoveAndSpawnPart ( Thing part )
        {
            if ( !this.childPartsIncluded.Contains( part ) )
            {
                Log.Error( "Tried to remove " + part.Label + " from " + this.parent.Label + " while it wasn't actually included." );
                return;
            }

            this.childPartsIncluded.Remove( part );
            this.DirtyCache();

            GenPlace.TryPlaceThing( part, this.parent.Position, this.parent.Map, ThingPlaceMode.Near );
        }

        // Save / Load

        public override void PostExposeData ()
        {
            base.PostExposeData();
            Scribe_Collections.Look<Thing>( ref this.childPartsIncluded, "childPartsIncluded", LookMode.Deep );
        }

        // Missing Parts

        private List<ThingDef> cachedMissingParts;

        public List<ThingDef> MissingParts
        {
            get
            {
                if ( cachedMissingParts == null )
                {
                    this.UpdateMissingParts();
                }

                return cachedMissingParts;
            }
        }

        protected void UpdateMissingParts ()
        {
            if ( cachedMissingParts == null )
            {
                cachedMissingParts = new List<ThingDef>();
            }
            else
            {
                cachedMissingParts.Clear();
            }

            if ( this.Props != null )
            {
                List<ThingDef> defsIncluded = new List<ThingDef>( from x in this.childPartsIncluded select x.def );

                foreach ( ThingDef expectedDef in this.Props.standardChildren )
                {
                    if ( defsIncluded.Contains( expectedDef ) )
                    {
                        defsIncluded.Remove( expectedDef );
                    }
                    else
                    {
                        cachedMissingParts.Add( expectedDef );
                    }
                }
            }
        }

        // Missing parts value

        private float cachedMissingValue = -1f;

        public float MissingValue
        {
            get
            {
                if ( cachedMissingValue == -1f )
                {
                    this.UpdateMissingValue();
                }

                return cachedMissingValue;
            }
        }

        protected float UpdateMissingValue ()
        {
            cachedMissingValue = 0f;

            foreach ( var missingPart in this.MissingParts )
            {
                cachedMissingValue += missingPart.BaseMarketValue * 0.8f;
            }

            foreach ( var subPart in this.childPartsIncluded )
            {
                var sComp = subPart.TryGetComp<CompIncludedChildParts>();

                if ( sComp != null )
                {
                    cachedMissingValue += sComp.MissingValue;
                }
            }

            return Mathf.Clamp( cachedMissingValue, 0f, this.parent.def.BaseMarketValue * 0.8f );
        }

        public void DirtyCache ()
        {
            cachedMissingParts = null;
            cachedMissingValue = -1f;
        }
    }
}