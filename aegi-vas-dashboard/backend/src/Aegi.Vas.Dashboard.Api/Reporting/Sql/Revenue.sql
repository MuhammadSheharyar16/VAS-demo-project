-- VAS revenue by country and centre in the reporting currency. Output: country, centre, revenue
SELECT r.country, r.centre, SUM(r.revenue_amount) AS revenue
FROM gold.vw_vas_revenue AS r
WHERE r.month_key BETWEEN @From AND @To
  AND (@Country IS NULL OR r.country = @Country)
  AND (@Centre  IS NULL OR r.centre  = @Centre)
GROUP BY r.country, r.centre;
