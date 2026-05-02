using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BioEngenharia
{
    // Configurações vindas do XML
    public class CompProperties_AutoCleaner : CompProperties
    {
        public float cleanRadius = 25f;
        public int ticksBetweenScan = 120;

        public CompProperties_AutoCleaner()
        {
            compClass = typeof(CompAutoCleaner);
        }
    }

    // Componente colocado no cachorro
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

            // Se já está ocupado, não interrompe
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

            // Manda o cachorro ir até aquela sujeira específica
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

    // Job que faz o cachorro andar até a sujeira e limpar somente ela
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

            // Vai até a sujeira
            yield return Toils_Goto.GotoThing(IndiceSujeira, PathEndMode.OnCell);

            // Espera um pouco em cima dela, simulando limpeza
            Toil limpar = Toils_General.Wait(120);
            limpar.WithProgressBarToilDelay(IndiceSujeira);

            limpar.tickAction = delegate
            {
                Pawn cachorro = pawn;

                // Efeito visual simples enquanto limpa
                if (cachorro.IsHashIntervalTick(30))
                {
                    FleckMaker.ThrowDustPuff(cachorro.Position.ToVector3Shifted(), cachorro.Map, 0.6f);
                }
            };

            yield return limpar;

            // Remove somente a sujeira alvo
            Toil removerSujeira = new Toil();
            removerSujeira.initAction = delegate
            {
                Thing coisa = job.GetTarget(IndiceSujeira).Thing;

                Filth sujeira = coisa as Filth;

                if (sujeira != null && sujeira.Spawned)
                {
                    // Só limpa se o cachorro estiver na mesma célula da sujeira
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