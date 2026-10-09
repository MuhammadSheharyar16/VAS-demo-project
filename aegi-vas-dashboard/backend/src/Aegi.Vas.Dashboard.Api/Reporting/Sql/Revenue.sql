-- VAS revenue by country, centre and visa type in the reporting currency. Output: country, centre, visa_type, revenue
SELECT r.country, r.centre, r.visa_type, SUM(r.revenue_amount) AS revenue
FROM gold.vw_vas_revenue AS r
WHERE r.month_key BETWEEN @From AND @To
  AND (@Country  IS NULL OR r.country   = @Country)
  AND (@Centre   IS NULL OR r.centre    = @Centre)
  AND (@VisaType IS NULL OR r.visa_type = @VisaType)
GROUP BY r.country, r.centre, r.visa_type;
