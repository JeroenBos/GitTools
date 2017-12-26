using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JBSnorro.GitTools.CI
{
    public sealed class TimingEstimator
    {
        public const int UnknownEstimate = int.MinValue;
        public int Estimate
        {
            get
            {
                if (estimateMultiplicity == 0)
                    return UnknownEstimate;
                return (int)Math.Ceiling(estimate);
            }
        }
        private double estimate;
        private int estimateMultiplicity;
        public void Add(int timing, TestResult result)
        {
            if (result == TestResult.Success)
            {
                this.estimate = (estimate * estimateMultiplicity + timing) / (estimateMultiplicity + 1);
                this.estimateMultiplicity++;
            }
        }
    }
}
