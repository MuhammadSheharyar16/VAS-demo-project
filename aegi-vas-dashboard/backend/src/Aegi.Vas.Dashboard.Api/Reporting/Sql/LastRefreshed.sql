-- When the gold layer was last refreshed. Output: last_refreshed_utc (datetime2, UTC)
SELECT MAX(s.last_refreshed_utc) AS last_refreshed_utc
FROM gold.vw_vas_refresh_status AS s;
