-- Countries and centres present in the reporting data, plus the month range. Output: country, centre, min_month, max_month
SELECT d.country, d.centre, MIN(d.month_key) AS min_month, MAX(d.month_key) AS max_month
FROM gold.vw_vas_visa_volume AS d
GROUP BY d.country, d.centre;
