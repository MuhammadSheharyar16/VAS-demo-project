-- Applications that reached each processing stage, by journey. Output: stage, stage_order, journey (centre|digital), applications
SELECT s.stage, s.stage_order, s.journey, SUM(s.application_count) AS applications
FROM gold.vw_vas_application_stages AS s
WHERE s.month_key BETWEEN @From AND @To
  AND (@Country  IS NULL OR s.country   = @Country)
  AND (@Centre   IS NULL OR s.centre    = @Centre)
  AND (@VisaType IS NULL OR s.visa_type = @VisaType)
GROUP BY s.stage, s.stage_order, s.journey
ORDER BY s.stage_order;
