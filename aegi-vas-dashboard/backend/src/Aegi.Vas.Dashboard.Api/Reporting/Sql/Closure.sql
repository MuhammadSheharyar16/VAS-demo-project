-- Closed vs total applications per month. Output: month, closed, total
SELECT c.month_key AS month, SUM(c.closed_count) AS closed, SUM(c.total_count) AS total
FROM gold.vw_vas_visa_closure AS c
WHERE c.month_key BETWEEN @From AND @To
  AND (@Country IS NULL OR c.country = @Country)
  AND (@Centre  IS NULL OR c.centre  = @Centre)
GROUP BY c.month_key
ORDER BY c.month_key;
