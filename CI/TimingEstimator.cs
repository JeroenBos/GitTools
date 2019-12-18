using System;

namespace JBSnorro.GitTools.CI
{
	public sealed class TimingEstimator
	{
		public const int UnknownEstimate = int.MinValue;
		private const int MovingAverageSize = 8;
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
				if (estimateMultiplicity == MovingAverageSize)
				{
					this.estimate = (estimate * (MovingAverageSize - 1) + timing) / MovingAverageSize;
				}
				else
				{
					this.estimate = (estimate * estimateMultiplicity + timing) / (estimateMultiplicity + 1);
					this.estimateMultiplicity++;
				}
			}
		}
	}
}
