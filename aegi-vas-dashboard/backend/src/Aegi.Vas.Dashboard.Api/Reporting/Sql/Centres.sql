-- Activity per centre. Output: country, centre, applications, centre_journey, processed, approved, turnaround_days, revenue, capacity
-- capacity is the centre's appointment capacity for the same months (NULL when not recorded). It is not split by visa type.
SELECT p.country, p.centre,
       SUM(p.application_count)     AS applications,
       SUM(p.centre_journey_count)  AS centre_journey,
       SUM(p.processed_count)       AS processed,
       SUM(p.approved_count)        AS approved,
       SUM(p.total_turnaround_days) AS turnaround_days,
       SUM(p.revenue_amount)        AS revenue,
       (SELECT SUM(k.capacity_count)
          FROM gold.vw_vas_centre_capacity AS k
         WHERE k.country = p.country AND k.centre = p.centre
           AND k.month_key BETWEEN @From AND @To) AS capacity
FROM gold.vw_vas_centre_performance AS p
WHERE p.month_key BETWEEN @From AND @To
  AND (@Country  IS NULL OR p.country   = @Country)
  AND (@Centre   IS NULL OR p.centre    = @Centre)
  AND (@VisaType IS NULL OR p.visa_type = @VisaType)
GROUP BY p.country, p.centre;
