# Caching

Status: infrastructure service available, application integration pending.

Redis is present in Compose but no cache key is read or written. Future cache-aside behavior will target dashboard and justified read models, with TTL, hit/miss metrics, invalidation and SQL fallback.
