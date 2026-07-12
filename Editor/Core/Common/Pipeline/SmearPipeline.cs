using System;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using SmearFramework.DataTypes;

namespace SmearFramework
{
    // Runs the smear frame pipeline: add stages, then RunAll() to execute them in order.
    public class SmearPipeline
    {
        private List<IPipelineStage> _stages;
        private PipelineContext _context;

        public PipelineContext Context => _context;
        public int StageCount => _stages.Count;

        // creates the context; diagnostics folder is only set up if config.ExportDiagnostics is on
        public SmearPipeline(PipelineConfig config, GameObject target, AnimationClip clip)
        {
            _stages = new List<IPipelineStage>();
            _context = new PipelineContext(config, target, clip);

            if (config.ExportDiagnostics)
            {
                var diag = new DiagnosticsData();
                diag.OutputFolder = config.DiagnosticsPath +
                    DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "_" + clip.name + "/";
                _context.Diagnostics = diag;
            }
        }

        // add a stage
        public void AddStage(IPipelineStage stage)
        {
            _stages.Add(stage);
            var grown = new float[_stages.Count];
            for (int i = 0; i < _context.StageTimes.Length; i++) grown[i] = _context.StageTimes[i];
            _context.StageTimes = grown;
        }

        // run all stages, flush diagnostics at the end if enabled
        public void RunAll()
        {
            for (int i = 0; i < _stages.Count; i++)
                RunStage(i);

            if (_context.Config.ExportDiagnostics && _context.Diagnostics != null)
                DiagnosticsExporter.Flush(_context.Diagnostics);
        }

        // run one stage and record elapsed ms
        public void RunStage(int index)
        {
            var sw = Stopwatch.StartNew();
            _stages[index].Execute(_context);
            sw.Stop();
            _context.StageTimes[index] = sw.ElapsedMilliseconds;
        }

        // run a slice of stages, startIndex..endIndex inclusive
        public void RunStages(int startIndex, int endIndex)
        {
            for (int i = startIndex; i <= endIndex; i++)
                RunStage(i);
        }

        // stage display name by index
        public string GetStageName(int index) => _stages[index].Name;

        public float TotalTimeMs
        {
            get
            {
                float total = 0;
                for (int i = 0; i < _context.StageTimes.Length; i++)
                    total += _context.StageTimes[i];
                return total;
            }
        }
    }
}
