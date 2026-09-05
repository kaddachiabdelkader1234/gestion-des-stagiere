package com.smartek.gateway.filter;

import org.springframework.cloud.gateway.filter.GatewayFilterChain;
import org.springframework.cloud.gateway.filter.GlobalFilter;
import org.springframework.core.Ordered;
import org.springframework.http.HttpHeaders;
import org.springframework.http.server.reactive.ServerHttpRequest;
import org.springframework.http.server.reactive.ServerHttpResponse;
import org.springframework.stereotype.Component;
import org.springframework.web.server.ServerWebExchange;
import reactor.core.publisher.Mono;

/**
 * Adds security headers to all responses.
 * Prevents common web vulnerabilities: clickjacking, MIME sniffing, XSS, etc.
 */
@Component
public class SecurityHeadersFilter implements GlobalFilter, Ordered {

    @Override
    public Mono<Void> filter(ServerWebExchange exchange, GatewayFilterChain chain) {
        ServerHttpResponse response = exchange.getResponse();
        HttpHeaders headers = response.getHeaders();

        // Prevent MIME type sniffing
        headers.add("X-Content-Type-Options", "nosniff");

        // Prevent clickjacking
        headers.add("X-Frame-Options", "DENY");

        // XSS protection (legacy but still useful for older browsers)
        headers.add("X-XSS-Protection", "1; mode=block");

        // Referrer policy
        headers.add("Referrer-Policy", "strict-origin-when-cross-origin");

        // Remove server header
        headers.remove("Server");

        return chain.filter(exchange);
    }

    @Override
    public int getOrder() {
        return -2; // Run before rate limiter
    }
}
