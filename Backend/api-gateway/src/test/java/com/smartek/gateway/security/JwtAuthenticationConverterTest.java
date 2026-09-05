package com.smartek.gateway.security;

import org.junit.jupiter.api.Test;
import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.oauth2.jwt.Jwt;
import reactor.core.publisher.Mono;
import reactor.test.StepVerifier;

import java.time.Instant;
import java.util.*;

import static org.assertj.core.api.Assertions.assertThat;

/**
 * Tests for JwtAuthenticationConverter.
 * auth-service emits a flat {@code role} claim holding a RoleType enum name.
 */
class JwtAuthenticationConverterTest {

    private final JwtAuthenticationConverter converter = new JwtAuthenticationConverter();

    @Test
    void convert_shouldExtractFlatRoleClaim() {
        Jwt jwt = createJwt(Map.of("sub", "admin@stb.tn", "role", "ADMIN"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication)).containsExactly("ROLE_ADMIN"))
            .verifyComplete();
    }

    @Test
    void convert_shouldExtractTrainerRole() {
        Jwt jwt = createJwt(Map.of("sub", "encadrant@stb.tn", "role", "TRAINER"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication)).containsExactly("ROLE_TRAINER"))
            .verifyComplete();
    }

    @Test
    void convert_shouldExtractLearnerRole() {
        Jwt jwt = createJwt(Map.of("sub", "stagiaire@stb.tn", "role", "LEARNER"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication)).containsExactly("ROLE_LEARNER"))
            .verifyComplete();
    }

    @Test
    void convert_shouldUppercaseRole() {
        Jwt jwt = createJwt(Map.of("sub", "admin@stb.tn", "role", "admin"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication)).containsExactly("ROLE_ADMIN"))
            .verifyComplete();
    }

    @Test
    void convert_shouldAlsoSupportListValuedRolesClaim() {
        Map<String, Object> claims = new HashMap<>();
        claims.put("sub", "multi@stb.tn");
        claims.put("roles", List.of("ADMIN", "TRAINER"));

        Jwt jwt = createJwt(claims);

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication))
                    .containsExactlyInAnyOrder("ROLE_ADMIN", "ROLE_TRAINER"))
            .verifyComplete();
    }

    @Test
    void convert_shouldHandleMissingRoleClaim() {
        Jwt jwt = createJwt(Map.of("sub", "nobody@stb.tn"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication -> {
                assertThat(authentication).isNotNull();
                assertThat(roleAuthorities(authentication)).isEmpty();
            })
            .verifyComplete();
    }

    @Test
    void convert_shouldIgnoreBlankRole() {
        Jwt jwt = createJwt(Map.of("sub", "blank@stb.tn", "role", "   "));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication ->
                assertThat(roleAuthorities(authentication)).isEmpty())
            .verifyComplete();
    }

    @Test
    void convert_shouldHandleMalformedRolesClaim() {
        Map<String, Object> claims = new HashMap<>();
        claims.put("sub", "weird@stb.tn");
        claims.put("roles", "not-a-list");

        Jwt jwt = createJwt(claims);

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication -> assertThat(authentication).isNotNull())
            .verifyComplete();
    }

    @Test
    void convert_shouldIncludeScopeAuthorities() {
        Jwt jwt = createJwt(Map.of("sub", "scoped@stb.tn", "scope", "openid profile email"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication -> assertThat(authentication.getAuthorities().stream()
                .map(GrantedAuthority::getAuthority)
                .filter(auth -> auth.startsWith("SCOPE_"))
                .toList()).isNotEmpty())
            .verifyComplete();
    }

    @Test
    void convert_shouldCombineScopeAndRole() {
        Jwt jwt = createJwt(Map.of("sub", "admin@stb.tn", "role", "ADMIN", "scope", "openid profile"));

        StepVerifier.create(converter.convert(jwt))
            .assertNext(authentication -> {
                List<String> all = authentication.getAuthorities().stream()
                    .map(GrantedAuthority::getAuthority)
                    .toList();

                assertThat(all).contains("ROLE_ADMIN");
                assertThat(all.stream().anyMatch(auth -> auth.startsWith("SCOPE_"))).isTrue();
            })
            .verifyComplete();
    }

    private static List<String> roleAuthorities(AbstractAuthenticationToken authentication) {
        return authentication.getAuthorities().stream()
            .map(GrantedAuthority::getAuthority)
            .filter(auth -> auth.startsWith("ROLE_"))
            .toList();
    }

    private Jwt createJwt(Map<String, Object> claims) {
        Instant now = Instant.now();

        return Jwt.withTokenValue("test-token")
            .header("alg", "HS256")
            .header("typ", "JWT")
            .claims(c -> c.putAll(claims))
            .issuedAt(now)
            .expiresAt(now.plusSeconds(3600))
            .build();
    }
}
