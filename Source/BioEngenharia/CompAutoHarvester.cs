using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BioEngenharia
{
    // ==============================
    // CONFIGURAÇÕES DO XML
    // ==============================
    public class CompProperties_AutoFarmer : CompProperties
    {
        public float workRadius = 50f;
        public int ticksBetweenScan = 60;
        public int workDuration = 180;

        public CompProperties_AutoFarmer()
        {
            compClass = typeof(CompAutoFarmer);
        }
    }

    // ==============================
    // COMPONENTE DO MACACO AGRICULTOR
    // ==============================
    public class CompAutoFarmer : ThingComp
    {
        private CompProperties_AutoFarmer Props
        {
            get { return (CompProperties_AutoFarmer)props; }
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn macaco = parent as Pawn;

            if (macaco == null)
                return;

            if (!macaco.Spawned || macaco.Map == null)
                return;

            if (macaco.Dead || macaco.Downed)
                return;

            if (!macaco.IsHashIntervalTick(Props.ticksBetweenScan))
                return;

            // Não interrompe se já estiver fazendo algum trabalho agrícola
            if (macaco.CurJob != null)
            {
                string jobAtual = macaco.CurJob.def.defName;

                if (jobAtual == "AutoHarvestPlantMonkey" ||
                    jobAtual == "AutoSowPlantMonkey" ||
                    jobAtual == "AutoCutPlantMonkey")
                {
                    return;
                }
            }

            // 1. Primeiro tenta colher plantas maduras
            Plant plantaParaColher = ProcurarPlantaParaColher(macaco);

            if (plantaParaColher != null)
            {
                DarJobColheita(macaco, plantaParaColher);
                return;
            }

            // 2. Depois tenta podar/remover planta errada na zona
            Plant plantaParaCortar = ProcurarPlantaErradaParaCortar(macaco);

            if (plantaParaCortar != null)
            {
                DarJobCorte(macaco, plantaParaCortar);
                return;
            }

            // 3. Depois tenta semear célula vazia
            IntVec3 celulaParaSemear;
            ThingDef plantaDef;

            if (ProcurarCelulaParaSemear(macaco, out celulaParaSemear, out plantaDef))
            {
                DarJobSemeadura(macaco, celulaParaSemear, plantaDef);
                return;
            }
        }

        // ==============================
        // PROCURAR ZONAS DE PLANTAÇÃO
        // ==============================
        private List<Zone_Growing> ObterZonasDePlantacao(Map map)
        {
            List<Zone_Growing> zonas = new List<Zone_Growing>();

            foreach (Zone zona in map.zoneManager.AllZones)
            {
                Zone_Growing zonaPlantacao = zona as Zone_Growing;

                if (zonaPlantacao != null)
                    zonas.Add(zonaPlantacao);
            }

            return zonas;
        }

        private Zone_Growing ObterZonaDaCelula(Map map, IntVec3 celula)
        {
            List<Zone_Growing> zonas = ObterZonasDePlantacao(map);

            foreach (Zone_Growing zona in zonas)
            {
                if (zona.Cells.Contains(celula))
                    return zona;
            }

            return null;
        }

        // ==============================
        // COLHER
        // ==============================
        private Plant ProcurarPlantaParaColher(Pawn macaco)
        {
            Map map = macaco.Map;
            IntVec3 posicao = macaco.Position;

            List<Thing> plantas = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);

            Plant melhorPlanta = null;
            float menorDistancia = 999999f;

            foreach (Thing thing in plantas)
            {
                Plant planta = thing as Plant;

                if (planta == null)
                    continue;

                if (!planta.Spawned || planta.Destroyed)
                    continue;

                if (!planta.HarvestableNow)
                    continue;

                // Só colhe se estiver dentro de uma zona de plantação
                Zone_Growing zona = ObterZonaDaCelula(map, planta.Position);

                if (zona == null)
                    continue;

                // Só colhe se for a planta correta daquela zona
                ThingDef plantaDaZona = zona.GetPlantDefToGrow();

                if (plantaDaZona != null && planta.def != plantaDaZona)
                    continue;

                // Respeita a área permitida do macaco
                if (!planta.Position.InAllowedArea(macaco))
                    continue;

                float distancia = planta.Position.DistanceTo(posicao);

                if (distancia > Props.workRadius)
                    continue;

                if (!macaco.CanReach(planta, PathEndMode.OnCell, Danger.Some))
                    continue;

                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhorPlanta = planta;
                }
            }

            return melhorPlanta;
        }

        private void DarJobColheita(Pawn macaco, Plant planta)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AutoHarvestPlantMonkey");

            if (jobDef == null)
            {
                Log.Error("[BioEngenharia] JobDef AutoHarvestPlantMonkey não encontrado.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, planta);
            macaco.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        // ==============================
        // PODAR / REMOVER PLANTA ERRADA
        // ==============================
        private Plant ProcurarPlantaErradaParaCortar(Pawn macaco)
        {
            Map map = macaco.Map;
            IntVec3 posicao = macaco.Position;

            List<Thing> plantas = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);

            Plant melhorPlanta = null;
            float menorDistancia = 999999f;

            foreach (Thing thing in plantas)
            {
                Plant planta = thing as Plant;

                if (planta == null)
                    continue;

                if (!planta.Spawned || planta.Destroyed)
                    continue;

                Zone_Growing zona = ObterZonaDaCelula(map, planta.Position);

                if (zona == null)
                    continue;

                ThingDef plantaDaZona = zona.GetPlantDefToGrow();

                if (plantaDaZona == null)
                    continue;

                // Se já é a planta certa da zona, não corta
                if (planta.def == plantaDaZona)
                    continue;

                // Respeita a área permitida do macaco
                if (!planta.Position.InAllowedArea(macaco))
                    continue;

                float distancia = planta.Position.DistanceTo(posicao);

                if (distancia > Props.workRadius)
                    continue;

                if (!macaco.CanReach(planta, PathEndMode.OnCell, Danger.Some))
                    continue;

                if (distancia < menorDistancia)
                {
                    menorDistancia = distancia;
                    melhorPlanta = planta;
                }
            }

            return melhorPlanta;
        }

        private void DarJobCorte(Pawn macaco, Plant planta)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AutoCutPlantMonkey");

            if (jobDef == null)
            {
                Log.Error("[BioEngenharia] JobDef AutoCutPlantMonkey não encontrado.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, planta);
            macaco.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        // ==============================
        // SEMEAR
        // ==============================
        private bool ProcurarCelulaParaSemear(Pawn macaco, out IntVec3 celulaEscolhida, out ThingDef plantaDefEscolhida)
        {
            celulaEscolhida = IntVec3.Invalid;
            plantaDefEscolhida = null;

            Map map = macaco.Map;
            IntVec3 posicao = macaco.Position;

            List<Zone_Growing> zonas = ObterZonasDePlantacao(map);

            float menorDistancia = 999999f;

            foreach (Zone_Growing zona in zonas)
            {
                ThingDef plantaDef = zona.GetPlantDefToGrow();

                if (plantaDef == null)
                    continue;

                foreach (IntVec3 celula in zona.Cells)
                {
                    if (!celula.InBounds(map))
                        continue;

                    // Respeita a área permitida do macaco
                    if (!celula.InAllowedArea(macaco))
                        continue;

                    float distancia = celula.DistanceTo(posicao);

                    if (distancia > Props.workRadius)
                        continue;

                    if (!macaco.CanReach(celula, PathEndMode.OnCell, Danger.Some))
                        continue;

                    // Se já tem planta, não semeia
                    Plant plantaExistente = celula.GetPlant(map);

                    if (plantaExistente != null && plantaExistente.Spawned)
                        continue;

                    // Se tem construção, não semeia
                    Building edificio = celula.GetEdifice(map);

                    if (edificio != null)
                        continue;

                    // Confere fertilidade mínima
                    TerrainDef terreno = map.terrainGrid.TerrainAt(celula);

                    if (terreno == null)
                        continue;

                    if (terreno.fertility < plantaDef.plant.fertilityMin)
                        continue;

                    if (distancia < menorDistancia)
                    {
                        menorDistancia = distancia;
                        celulaEscolhida = celula;
                        plantaDefEscolhida = plantaDef;
                    }
                }
            }

            return celulaEscolhida.IsValid && plantaDefEscolhida != null;
        }

        private void DarJobSemeadura(Pawn macaco, IntVec3 celula, ThingDef plantaDef)
        {
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("AutoSowPlantMonkey");

            if (jobDef == null)
            {
                Log.Error("[BioEngenharia] JobDef AutoSowPlantMonkey não encontrado.");
                return;
            }

            Job job = JobMaker.MakeJob(jobDef, celula);
            job.plantDefToSow = plantaDef;

            macaco.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    // ==============================
    // JOB DE COLHEITA
    // ==============================
    public class JobDriver_AutoHarvestPlantMonkey : JobDriver
    {
        private const TargetIndex IndicePlanta = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(IndicePlanta);
            this.FailOn(() => !pawn.Position.InAllowedArea(pawn));

            yield return Toils_Goto.GotoThing(IndicePlanta, PathEndMode.OnCell);

            Toil colher = Toils_General.Wait(180);
            colher.WithProgressBarToilDelay(IndicePlanta);

            colher.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(30))
                {
                    FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted(), pawn.Map, 0.3f);
                }
            };

            yield return colher;

            Toil finalizarColheita = new Toil();
            finalizarColheita.initAction = delegate
            {
                Plant planta = job.GetTarget(IndicePlanta).Thing as Plant;

                if (planta == null)
                    return;

                if (!planta.Spawned || planta.Destroyed)
                    return;

                if (!planta.HarvestableNow)
                    return;

                if (!planta.Position.InAllowedArea(pawn))
                    return;

                ThingDef produto = planta.def.plant.harvestedThingDef;
                int quantidade = planta.YieldNow();

                if (produto != null && quantidade > 0)
                {
                    Thing itemColhido = ThingMaker.MakeThing(produto);
                    itemColhido.stackCount = quantidade;

                    GenPlace.TryPlaceThing(
                        itemColhido,
                        planta.Position,
                        planta.Map,
                        ThingPlaceMode.Near
                    );
                }

                planta.Destroy();
            };

            finalizarColheita.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalizarColheita;
        }
    }

    // ==============================
    // JOB DE SEMEADURA
    // ==============================
    public class JobDriver_AutoSowPlantMonkey : JobDriver
    {
        private const TargetIndex IndiceCelula = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !pawn.Position.InAllowedArea(pawn));

            yield return Toils_Goto.GotoCell(IndiceCelula, PathEndMode.OnCell);

            Toil semear = Toils_General.Wait(180);
            semear.WithProgressBarToilDelay(IndiceCelula);

            semear.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(30))
                {
                    FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted(), pawn.Map, 0.25f);
                }
            };

            yield return semear;

            Toil finalizarSemeadura = new Toil();
            finalizarSemeadura.initAction = delegate
            {
                IntVec3 celula = job.GetTarget(IndiceCelula).Cell;
                Map map = pawn.Map;

                if (!celula.InBounds(map))
                    return;

                if (!celula.InAllowedArea(pawn))
                    return;

                if (celula.GetPlant(map) != null)
                    return;

                if (celula.GetEdifice(map) != null)
                    return;

                ThingDef plantaDef = job.plantDefToSow;

                if (plantaDef == null)
                    return;

                Plant novaPlanta = ThingMaker.MakeThing(plantaDef) as Plant;

                if (novaPlanta == null)
                    return;

                novaPlanta.Growth = 0.05f;
                novaPlanta.sown = true;

                GenSpawn.Spawn(novaPlanta, celula, map);
            };

            finalizarSemeadura.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalizarSemeadura;
        }
    }

    // ==============================
    // JOB DE PODA / CORTE
    // ==============================
    public class JobDriver_AutoCutPlantMonkey : JobDriver
    {
        private const TargetIndex IndicePlanta = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(IndicePlanta);
            this.FailOn(() => !pawn.Position.InAllowedArea(pawn));

            yield return Toils_Goto.GotoThing(IndicePlanta, PathEndMode.OnCell);

            Toil cortar = Toils_General.Wait(150);
            cortar.WithProgressBarToilDelay(IndicePlanta);

            cortar.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(30))
                {
                    FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted(), pawn.Map, 0.25f);
                }
            };

            yield return cortar;

            Toil finalizarCorte = new Toil();
            finalizarCorte.initAction = delegate
            {
                Plant planta = job.GetTarget(IndicePlanta).Thing as Plant;

                if (planta == null)
                    return;

                if (!planta.Spawned || planta.Destroyed)
                    return;

                if (!planta.Position.InAllowedArea(pawn))
                    return;

                planta.Destroy();
            };

            finalizarCorte.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalizarCorte;
        }
    }
}