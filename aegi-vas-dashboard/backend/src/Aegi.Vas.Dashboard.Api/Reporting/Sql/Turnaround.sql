-- Average days from application to decision by visa type for one journey. Output: visa_type, avg_days, applications
-- Weighted by application count so small centres do not skew the average.
SELECT t.visa_type,
       CAST(SUM(t.total_turnaround_days) AS decimal(18,4)) / NULLIF(SUM(t.application_count), 0) AS avg_days,
       SUM(t.application_count) AS applications
FROM gold.vw_vas_turnaround AS t
WHERE t.month_key BETWEEN @From AND @To
  AND t.journey = @Journey
  AND (@Country  IS NULL OR t.country   = @Country)
  AND (@Centre   IS NULL OR t.centre    = @Centre)
  AND (@VisaType IS NULL OR t.visa_type = @VisaType)
GROUP BY t.visa_type;
