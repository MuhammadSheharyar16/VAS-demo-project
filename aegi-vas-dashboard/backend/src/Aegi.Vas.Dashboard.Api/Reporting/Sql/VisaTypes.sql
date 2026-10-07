-- Volume and outcomes per visa type. Output: visa_type, volume, approved, rejected, pending, closed
SELECT p.visa_type,
       SUM(p.visa_count)     AS volume,
       SUM(p.approved_count) AS approved,
       SUM(p.rejected_count) AS rejected,
       SUM(p.pending_count)  AS pending,
       SUM(p.closed_count)   AS closed
FROM gold.vw_vas_visa_type_performance AS p
WHERE p.month_key BETWEEN @From AND @To
  AND (@Country IS NULL OR p.country = @Country)
  AND (@Centre  IS NULL OR p.centre  = @Centre)
GROUP BY p.visa_type;
