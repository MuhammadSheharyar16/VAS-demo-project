-- Applications per month and journey. Output: month (yyyy-MM), journey (centre|digital), visas
SELECT v.month_key AS month, v.journey, SUM(v.visa_count) AS visas
FROM gold.vw_vas_visa_volume AS v
WHERE v.month_key BETWEEN @From AND @To
  AND (@Country  IS NULL OR v.country   = @Country)
  AND (@Centre   IS NULL OR v.centre    = @Centre)
  AND (@VisaType IS NULL OR v.visa_type = @VisaType)
GROUP BY v.month_key, v.journey
ORDER BY v.month_key;
