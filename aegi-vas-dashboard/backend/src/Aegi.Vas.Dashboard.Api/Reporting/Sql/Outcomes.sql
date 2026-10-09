-- Applications by outcome. Output: outcome, visas
SELECT o.outcome, SUM(o.visa_count) AS visas
FROM gold.vw_vas_visa_outcomes AS o
WHERE o.month_key BETWEEN @From AND @To
  AND (@Country  IS NULL OR o.country   = @Country)
  AND (@Centre   IS NULL OR o.centre    = @Centre)
  AND (@VisaType IS NULL OR o.visa_type = @VisaType)
GROUP BY o.outcome;
