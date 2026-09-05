package com.smartek.gateway.filter;

import org.springframework.cloud.gateway.filter.GatewayFilterChain;
import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.core.Ordered;
import org.springframework.http.HttpStatus;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.http.server.reactive.ServerHttpResponse;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import reactor.core.publisher.Mono;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Global rate limiter with two tiers:
 * <ul>
 *   <li>Auth endpoints (login, register, refresh): 10 requests/min per IP — anti-brute-force.</li>
 *   <li>General API endpoints (/api/v1/**): 60 requests/min per IP — blocks scraping, allows normal UI usage.</li>
 * </ul>
 */
@Component
public class RateLimitFilter implements GlobalFilter, Ordered {

    private static final int AUTH_LIMIT = 10;
    private static final int GENERAL_LIMIT = 60;
    private static final long WINDOW_MS = 60_000; // 1 minute

    private final Map<String, RequestCounter> authCounters = new ConcurrentHashMap<>();
    private final Map<String, RequestCounter> generalCounters = new ConcurrentHashMap<>();

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, GatewayFilterChain chain) {
        ServerHttpRequest request = exchange.getRequest();
        String path = request.getURI().getPath();

        if (isAuthEndpoint(path)) {
            return checkRateLimit(exchange, chain, authCounters, AUTH_LIMIT);
        }

        if (isGeneralApi(path)) {
            return checkRateLimit(exchange, chain, generalCounters, GENERAL_LIMIT);
        }

        return chain.filter(exchange);
    }

    private Mono<Void> checkRateLimit(
            ServerWebExchange exchange,
            GatewayFilterChain chain,
            Map<String, RequestCounter> counters,
            int limit) {

        String clientIp = getClientIp(exchange.getRequest());
        long now = System.currentTimeMillis();

        RequestCounter counter = counters.compute(clientIp, (key, existing) -> {
            if (existing == null || now - existing.windowStart > WINDOW_MS) {
                return new RequestCounter(now);
            }
            return existing;
        });

        if (counter.count.incrementAndGet() > limit) {
            ServerHttpResponse response = exchange.getResponse();
            response.setStatusCode(HttpStatus.TOO_MANY_REQUESTS);
            response.getHeaders().add("Retry-After", "60");
            return response.setComplete();
        }

        return chain.filter(exchange);
    }

    private boolean isAuthEndpoint(String path) {
        return path.contains("/api/v1/auth/login")
            || path.contains("/api/v1/auth/register")
            || path.contains("/api/v1/auth/refresh")
            || path.contains("/api/auth/login")
            || path.contains("/api/auth/register");
    }

    private boolean isGeneralApi(String path) {
        return path.startsWith("/api/v1/") && !isAuthEndpoint(path);
    }

    private String getClientIp(ServerHttpRequest request) {
        if (request.getRemoteAddress() != null && request.getRemoteAddress().getAddress() != null) {
            return request.getRemoteAddress().getAddress().getHostAddress();
        }
        return "unknown";
    }

    @Override
    public int getOrder() {
        return -1; // Run after TraceIdFilter (-3) and SecurityHeadersFilter (-2)
    }

    private static class RequestCounter {
        final long windowStart;
        final AtomicInteger count;

        RequestCounter(long windowStart) {
            this.windowStart = windowStart;
            this.count = new AtomicInteger(0);
        }
    }
}
