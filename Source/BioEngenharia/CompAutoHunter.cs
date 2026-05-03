using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BioEngenharia
{
    // ==============================
    // CONFIGURAÇÕES DO XML
    // ==============================
    public class CompProperties_AutoHunter : CompProperties
    {
        public float huntRadius = 35f;
        public int ticksBetweenScan = 180;
        public float maxPreyBodySize = 0.45f;

        public CompProperties_AutoHunter()
        {
            compClass = typeof(CompAutoHunter);
        }
    }

    // ==============================
    // COMPONENTE DO PUMA CAÇADOR
    // ==============================
    public class CompAutoHunter : ThingComp
    {
        private CompProperties_AutoHunter Props
        {
            get { return (CompProperties_AutoHunter)props; }
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn cacador = parent as Pawn;

            if (cacador == null)
                return;

            if (!cacador.Spawned || cacador.Map == null)
                return;

            if (cacador.Dead || cacador.Downed)
                return;

            if (!cacador.IsHashIntervalTick(Props.ticksBetweenScan))
                return;

            // Se já está atacando, não interrompe
            if (cacador.CurJob != null && cacador.CurJob.def == JobDefOf.AttackMelee)
                return;

            Pawn presa = ProcurarPresaMaisProxima(cacador);

            if (presa == null)
                return;

            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, presa);

            job.expiryInterval = 2000;
            job.checkOverrideOnExpire = true;

            cacador.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private Pawn ProcurarPresaMaisProxima(Pawn cacador)
        {
            Map map = cacador.Map;
            IntVec3 posicao = cacador.Position;

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;

            Pawn melhorPresa = null;
            float menorDistancia = 999999f;

            foreach (Pawn presa in pawns)
            {
                if (presa == null)
                    continue;

                if (!presa.Spawned)
                    continue;

                if (presa.Dead || presa.Downed)
                    continue;

                // Não caça a si mesmo
                if (presa == cacador)
                    continue;

                // Só caça animais
                if (presa.def == null || presa.def.race == null || !presa.def.race.Animal)
                    continue;

                // Nunca caça humanos
                if (presa.RaceProps != null && presa.RaceProps.Humanlike)
                    continue;

                // Não caça animais da colônia ou de facções
                if (presa.Faction != null)
                    continue;

                // Não caça animais domesticados
                if (presa.Faction == Faction.OfPlayer)
                    continue;

                // Não caça animais grandes
                if (presa.BodySize > Props.maxPreyBodySize)
                    continue;

                // Respeita área permitida do puma
                if (!presa.Position.InAllowedArea(cacador))
                    continue;

                float distancia = presa.Position.DistanceTo(posicao);

                if (distancia > Props.huntRadius)
                    continue;

                if (!cacador.CanReach(presa, PathEndMode.Touch, Danger.Some))
                    continue;

                // Evita escolher presas muito perigosas
                if (presa.kindDef != null && presa.kindDef.combatPower > 80)
                    continue;

                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhorPresa = presa;
                }
            }

            return melhorPresa;
        }
    }
}