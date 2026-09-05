package com.smartek.gateway.security;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.security.config.annotation.web.reactive.EnableWebFluxSecurity;
import org.springframework.security.config.web.server.ServerHttpSecurity;
import org.springframework.security.oauth2.jose.jws.MacAlgorithm;
import org.springframework.security.oauth2.jwt.NimbusReactiveJwtDecoder;
import org.springframework.security.oauth2.jwt.ReactiveJwtDecoder;
import org.springframework.security.web.server.SecurityWebFilterChain;
import org.springframework.security.web.server.authentication.HttpStatusServerEntryPoint;
import org.springframework.security.web.server.authorization.HttpStatusServerAccessDeniedHandler;
import org.springframework.web.cors.CorsConfiguration;
import org.springframework.web.cors.reactive.CorsConfigurationSource;
import org.springframework.web.cors.reactive.UrlBasedCorsConfigurationSource;

import javax.crypto.spec.SecretKeySpec;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.List;

/**
 * Security configuration for the API Gateway.
 *
 * Tokens are HS256 JWTs issued by auth-service, signed with the shared {@code JWT_SECRET}.
 * The role is carried in a flat {@code role} claim (ADMIN / TRAINER / LEARNER) — see
 * {@link JwtAuthenticationConverter}.
 *
 * Rule order matters: Spring Security applies the first matching {@code pathMatchers} entry, so
 * the narrow rules (journal sub-resource, auth lookups) are declared before the broad ones.
 */
@Configuration
@EnableWebFluxSecurity
public class SecurityConfig {

    private static final String ROLE_ADMIN = "ROLE_ADMIN";
    private static final String ROLE_TRAINER = "ROLE_TRAINER";
    private static final String ROLE_LEARNER = "ROLE_LEARNER";

    private final String jwtSecret;
    private final List<String> allowedOrigins;

    public SecurityConfig(
            @Value("${jwt.secret}") String jwtSecret,
            @Value("${gateway.cors.allowed-origins:http://localhost:4200}") List<String> allowedOrigins) {
        this.jwtSecret = jwtSecret;
        this.allowedOrigins = allowedOrigins;
    }

    @Bean
    public SecurityWebFilterChain securityWebFilterChain(ServerHttpSecurity http) {
        JwtAuthenticationConverter jwtConverter = new JwtAuthenticationConverter();

        http
            // Stateless REST API — no CSRF tokens to protect.
            .csrf(csrf -> csrf.disable())

            .cors(cors -> cors.configurationSource(corsConfigurationSource()))

            .authorizeExchange(authorize -> authorize
                // Public — no authentication required
                .pathMatchers(HttpMethod.OPTIONS, "/**").permitAll()
                .pathMatchers("/actuator/health/**", "/actuator/info", "/actuator/prometheus").permitAll()

                // Auth — only the anonymous entry points are public. A caller without a token
                // must be able to register and log in, and the health probe stays open.
                .pathMatchers(HttpMethod.POST, "/api/v1/auth/register", "/api/v1/auth/login", "/api/v1/auth/refresh").permitAll()
                .pathMatchers(HttpMethod.GET, "/api/v1/auth/health").permitAll()
                // /user/{userId} and /validate/{userId} take a guessable numeric id and return
                // account details (email, role, profile image, experience). /users?role=… lists
                // whole accounts. Blanket permit-all on /api/v1/auth/** made the user table
                // enumerable anonymously — restrict to admin, the only role that needs to look up
                // other accounts (e.g. to pick an encadrant).
                .pathMatchers("/api/v1/auth/user/**", "/api/v1/auth/validate/**", "/api/v1/auth/users")
                    .hasAuthority(ROLE_ADMIN)
                // Admin-only: create an encadrant account
                .pathMatchers(HttpMethod.POST, "/api/v1/auth/encadrants")
                    .hasAuthority(ROLE_ADMIN)
                .pathMatchers("/api/v1/auth/**").authenticated()

                // Candidatures — a learner submits and tracks their own; only an admin decides.
                // Ownership is additionally enforced inside Stagiaire.Service from the JWT, so a
                // learner cannot read another's candidature even though the path is shared.
                .pathMatchers(HttpMethod.POST, "/api/v1/candidatures/*/accepter",
                                               "/api/v1/candidatures/*/rejeter")
                    .hasAuthority(ROLE_ADMIN)
                .pathMatchers(HttpMethod.POST, "/api/v1/candidatures/*/cv",
                                               "/api/v1/candidatures/*/document")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_LEARNER)
                .pathMatchers(HttpMethod.POST, "/api/v1/candidatures")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_LEARNER)
                .pathMatchers(HttpMethod.GET, "/api/v1/candidatures/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)

                // Stagiaires — admins manage the roster; trainers and learners read their own view.
                .pathMatchers(HttpMethod.DELETE, "/api/v1/stagiaires/**").hasAuthority(ROLE_ADMIN)
                .pathMatchers(HttpMethod.POST, "/api/v1/stagiaires/*/journal/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                .pathMatchers(HttpMethod.PUT, "/api/v1/stagiaires/*/journal/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                .pathMatchers(HttpMethod.GET, "/api/v1/stagiaires/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                // A learner submits their own candidature; admin creates/edits records directly.
                .pathMatchers(HttpMethod.POST, "/api/v1/stagiaires/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_LEARNER)
                .pathMatchers(HttpMethod.PUT, "/api/v1/stagiaires/**").hasAuthority(ROLE_ADMIN)

                // Conventions — admin drafts and signs; everyone authenticated can read/download.
                .pathMatchers(HttpMethod.GET, "/api/v1/conventions/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                .pathMatchers("/api/v1/conventions/**").hasAuthority(ROLE_ADMIN)

                // Evaluations — trainers submit, admin oversees, learners read their result.
                .pathMatchers(HttpMethod.GET, "/api/v1/evaluations/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                .pathMatchers(HttpMethod.DELETE, "/api/v1/evaluations/**").hasAuthority(ROLE_ADMIN)
                .pathMatchers("/api/v1/evaluations/**").hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER)

                // Notifications — readable by the recipient; only admin may mutate.
                .pathMatchers(HttpMethod.GET, "/api/v1/notifications/**")
                    .hasAnyAuthority(ROLE_ADMIN, ROLE_TRAINER, ROLE_LEARNER)
                .pathMatchers("/api/v1/notifications/**").hasAuthority(ROLE_ADMIN)

                .anyExchange().authenticated()
            )

            .oauth2ResourceServer(oauth2 -> oauth2
                .jwt(jwt -> jwt.jwtAuthenticationConverter(jwtConverter))
                .authenticationEntryPoint(new HttpStatusServerEntryPoint(HttpStatus.UNAUTHORIZED))
                .accessDeniedHandler(new HttpStatusServerAccessDeniedHandler(HttpStatus.FORBIDDEN))
            );

        return http.build();
    }

    /**
     * Decodes the HS256 tokens minted by auth-service using the shared symmetric secret.
     * Replaces the previous Keycloak JWKS decoder, which could never validate these tokens.
     */
    @Bean
    public ReactiveJwtDecoder reactiveJwtDecoder() {
        SecretKeySpec key = new SecretKeySpec(jwtSecret.getBytes(StandardCharsets.UTF_8), "HmacSHA256");

        return NimbusReactiveJwtDecoder.withSecretKey(key)
            .macAlgorithm(MacAlgorithm.HS256)
            .build();
    }

    /**
     * CORS configuration to allow Angular frontend access.
     */
    @Bean
    public CorsConfigurationSource corsConfigurationSource() {
        CorsConfiguration configuration = new CorsConfiguration();

        configuration.setAllowedOrigins(allowedOrigins);

        configuration.setAllowedMethods(Arrays.asList(
            HttpMethod.GET.name(),
            HttpMethod.POST.name(),
            HttpMethod.PUT.name(),
            HttpMethod.PATCH.name(),
            HttpMethod.DELETE.name(),
            HttpMethod.OPTIONS.name()
        ));

        // Allow all headers (includes Authorization)
        configuration.setAllowedHeaders(List.of("*"));

        // Expose Content-Disposition so the browser can name downloaded convention/attestation PDFs.
        configuration.setExposedHeaders(List.of("Content-Disposition"));

        configuration.setAllowCredentials(true);

        // Cache preflight response for 1 hour
        configuration.setMaxAge(3600L);

        UrlBasedCorsConfigurationSource source = new UrlBasedCorsConfigurationSource();
        source.registerCorsConfiguration("/**", configuration);

        return source;
    }
}
