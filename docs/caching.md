# Caching

Status: infrastructure service available, application integration pending.

Redis is present in Compose but no cache key is read or written. Dashboard and reporting currently query SQL Server directly so their correctness and execution plans can be studied without cache effects. Future cache-aside behavior may target these read models, with store/filter-aware keys, TTL, hit/miss metrics, invalidation and SQL fallback.
