using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BioEngenharia
{
    // ==============================
    // CONFIGURAÇÕES DO XML
    // ==============================
    public class CompProperties_AutoCleaner : CompProperties
    {
        public float cleanRadius = 25f;
        public int ticksBetweenScan = 120;

        public CompProperties_AutoCleaner()
        {
            compClass = typeof(CompAutoCleaner);
        }
    }

    // ==============================
    // COMPONENTE DO CACHORRO
    // ==============================
    public class CompAutoCleaner : ThingComp
    {
        private CompProperties_AutoCleaner Props
        {
            get { return (CompProperties_AutoCleaner)props; }
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;

            if (pawn == null)
                return;

            if (!pawn.Spawned || pawn.Map == null)
                return;

            if (pawn.Dead || pawn.Downed)
                return;

            if (!pawn.IsHashIntervalTick(Props.ticksBetweenScan))
                return;

            // Não interrompe se já estiver limpando
            if (pawn.CurJob != null && pawn.CurJob.def.defName == "AutoCleanFilthDog")
                return;

            Filth sujeira = ProcurarSujeiraMaisProxima(pawn);

            if (sujeira == null)
                return;

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AutoCleanFilthDog");

            if (jobDef == null)
            {
                Log.Error("[BioEngenharia] JobDef AutoCleanFilthDog não encontrado.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, sujeira);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private Filth ProcurarSujeiraMaisProxima(Pawn pawn)
        {
            Map map = pawn.Map;
            IntVec3 posicao = pawn.Position;

            List<Thing> sujeiras = map.listerThings.ThingsInGroup(ThingRequestGroup.Filth);

            Filth melhorSujeira = null;
            float menorDistancia = 999999f;

            foreach (Thing thing in sujeiras)
            {
                Filth sujeira = thing as Filth;

                if (sujeira == null)
                    continue;

                if (!sujeira.Spawned)
                    continue;

                // 🔒 RESPEITA ZONA PERMITIDA
                if (!sujeira.Position.InAllowedArea(pawn))
                    continue;

                float distancia = sujeira.Position.DistanceTo(posicao);

                if (distancia > Props.cleanRadius)
                    continue;

                if (!pawn.CanReach(sujeira, PathEndMode.OnCell, Danger.Some))
                    continue;

                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhorSujeira = sujeira;
                }
            }

            return melhorSujeira;
        }
    }

    // ==============================
    // JOB DE LIMPEZA
    // ==============================
    public class JobDriver_AutoCleanFilthDog : JobDriver
    {
        private const TargetIndex IndiceSujeira = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(IndiceSujeira);

            // 🔒 NÃO SAIR DA ZONA
            this.FailOn(() => !pawn.Position.InAllowedArea(pawn));

            // Ir até a sujeira
            yield return Toils_Goto.GotoThing(IndiceSujeira, PathEndMode.OnCell);

            // Tempo de limpeza
            Toil limpar = Toils_General.Wait(120);
            limpar.WithProgressBarToilDelay(IndiceSujeira);

            // Efeito visual de limpeza igual ao dos colonos
            EffecterDef efeitoLimpeza = DefDatabase<EffecterDef>.GetNamedSilentFail("Clean");

            if (efeitoLimpeza != null)
            {
                limpar.WithEffect(efeitoLimpeza, IndiceSujeira);
            }

            limpar.tickAction = delegate
            {
                // Poeirinha extra leve, opcional
                if (pawn.IsHashIntervalTick(30))
                {
                    FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted(), pawn.Map, 0.4f);
                }
            };

            yield return limpar;

            // Remover a sujeira SOMENTE se estiver em cima
            Toil removerSujeira = new Toil();
            removerSujeira.initAction = delegate
            {
                Thing coisa = job.GetTarget(IndiceSujeira).Thing;
                Filth sujeira = coisa as Filth;

                if (sujeira != null && sujeira.Spawned)
                {
                    if (pawn.Position == sujeira.Position)
                    {
                        sujeira.Destroy();
                        FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted(), pawn.Map, 0.9f);
                    }
                }
            };

            removerSujeira.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return removerSujeira;
        }
    }
}