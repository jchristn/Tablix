namespace Tablix.Server.Observability
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Tablix.Core.Models;
    using Tablix.Core.Observability;

    /// <summary>
    /// Tracks the stages of one crawl pipeline job (queued, discover, examine, cache). Each stage gets a
    /// "stage:{name}" child span of the job span and a <see cref="TelemetryNames.MetricCrawlStageDuration"/>
    /// measurement. Stage transitions are driven by the crawler's progress callbacks, which this tracker wraps.
    /// Stage spans never change <see cref="Activity.Current"/>.
    /// Thread safety: not thread-safe; one tracker belongs to one crawl job, whose callbacks run sequentially.
    /// </summary>
    public class CrawlStageTracker
    {
        #region Private-Members

        private readonly Activity _Job;
        private readonly string _DbSystem;
        private string _Stage = null;
        private long _StageStart = 0;
        private Activity _StageActivity = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="job">The crawl job span. May be null when tracing is off; metrics are still recorded.</param>
        /// <param name="dbSystem">Database system name label.</param>
        public CrawlStageTracker(Activity job, string dbSystem)
        {
            _Job = job;
            _DbSystem = dbSystem ?? TelemetryNames.ValueUnknown;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record time the job spent queued behind earlier jobs in a batch, ending now.
        /// </summary>
        /// <param name="queuedSince">When the job became eligible to run.</param>
        public void RecordQueued(DateTimeOffset queuedSince)
        {
            try
            {
                double seconds = Math.Max(0, (DateTimeOffset.UtcNow - queuedSince).TotalSeconds);
                Activity queued = TablixTracing.StartDetachedChild(TablixTracing.StageSpanName(TelemetryNames.StageQueued), _Job, queuedSince);
                TablixTracing.SetSuccess(queued);
                TablixTracing.StopDetached(queued);
                TablixMetrics.RecordCrawlStage(TelemetryNames.StageQueued, _DbSystem, TelemetryNames.OutcomeSuccess, seconds);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Complete the current stage successfully (if any) and begin <paramref name="stage"/>.
        /// </summary>
        /// <param name="stage">Stage name.</param>
        public void Begin(string stage)
        {
            if (_Stage == stage) return;

            EndCurrent(TelemetryNames.OutcomeSuccess, null);
            _Stage = stage;
            _StageStart = Stopwatch.GetTimestamp();
            _StageActivity = TablixTracing.StartDetachedChild(TablixTracing.StageSpanName(stage), _Job);
        }

        /// <summary>
        /// Complete the current stage with an outcome. Safe to call more than once.
        /// </summary>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="ex">Exception that ended the stage, or null.</param>
        public void Complete(string outcome, Exception ex)
        {
            EndCurrent(outcome, ex);
        }

        /// <summary>
        /// Wrap a crawler progress callback so crawler progress drives stage transitions. The original callback,
        /// when present, is invoked unchanged.
        /// </summary>
        /// <param name="inner">Original callback. May be null.</param>
        /// <returns>The wrapping callback.</returns>
        public Func<CrawlProgressUpdate, Task> Wrap(Func<CrawlProgressUpdate, Task> inner)
        {
            return async (update) =>
            {
                OnProgress(update);
                if (inner != null) await inner(update).ConfigureAwait(false);
            };
        }

        #endregion

        #region Private-Methods

        private void OnProgress(CrawlProgressUpdate update)
        {
            try
            {
                if (update == null) return;
                if (String.Equals(update.Stage, "tables_discovered", StringComparison.Ordinal))
                {
                    TablixTracing.SetTag(_Job, TelemetryNames.AttrTableCount, update.TableCount ?? 0);
                    Begin(TelemetryNames.StageExamine);
                }
            }
            catch (Exception)
            {
            }
        }

        private void EndCurrent(string outcome, Exception ex)
        {
            if (_Stage == null) return;

            try
            {
                if (ex != null) TablixTracing.RecordException(_StageActivity, ex);
                else if (outcome == TelemetryNames.OutcomeSuccess) TablixTracing.SetSuccess(_StageActivity);
                else TablixTracing.SetOutcome(_StageActivity, outcome, null);

                TablixTracing.StopDetached(_StageActivity);
                TablixMetrics.RecordCrawlStage(_Stage, _DbSystem, outcome, TablixMetrics.SecondsSince(_StageStart));
            }
            catch (Exception)
            {
            }
            finally
            {
                _Stage = null;
                _StageActivity = null;
            }
        }

        #endregion
    }
}
