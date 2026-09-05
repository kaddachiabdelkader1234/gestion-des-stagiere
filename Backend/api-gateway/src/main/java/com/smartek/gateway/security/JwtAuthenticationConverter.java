package com.smartek.gateway.security;

import org.springframework.core.convert.converter.Converter;
import org.springframework.security.authentication.AbstractAuthenticationToken;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.oauth2.jwt.Jwt;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.security.oauth2.server.resource.authentication.JwtGrantedAuthoritiesConverter;
import reactor.core.publisher.Mono;

import java.util.*;
import java.util.stream.Collectors;
import java.util.stream.Stream;

/**
 * Converts auth-service JWTs into Spring Security authentications.
 *
 * auth-service emits a single flat {@code role} claim holding the {@code RoleType} enum name
 * (ADMIN, TRAINER, LEARNER, ...) — see AuthService#login. Each role becomes a ROLE_-prefixed
 * authority so {@code hasAuthority("ROLE_ADMIN")} works in {@link SecurityConfig}.
 *
 * A list-valued {@code roles} claim is also accepted so a future multi-role token keeps working.
 */
public class JwtAuthenticationConverter implements Converter<Jwt, Mono<AbstractAuthenticationToken>> {

    private static final String ROLE_CLAIM = "role";
    private static final String ROLES_CLAIM = "roles";
    private static final String ROLE_PREFIX = "ROLE_";

    private final JwtGrantedAuthoritiesConverter defaultGrantedAuthoritiesConverter = new JwtGrantedAuthoritiesConverter();

    @Override
    public Mono<AbstractAuthenticationToken> convert(Jwt jwt) {
        Collection<GrantedAuthority> authorities = extractAuthorities(jwt);
        return Mono.just(new JwtAuthenticationToken(jwt, authorities));
    }

    /**
     * Combines standard OAuth2 scope authorities with the role claim(s).
     */
    private Collection<GrantedAuthority> extractAuthorities(Jwt jwt) {
        Collection<GrantedAuthority> scopeAuthorities = defaultGrantedAuthoritiesConverter.convert(jwt);

        return Stream.concat(
            scopeAuthorities != null ? scopeAuthorities.stream() : Stream.<GrantedAuthority>empty(),
            extractRoles(jwt).stream()
        ).collect(Collectors.toSet());
    }

    /**
     * Reads the singular {@code role} claim and, if present, a list-valued {@code roles} claim.
     */
    private Collection<GrantedAuthority> extractRoles(Jwt jwt) {
        try {
            Stream<String> singular = Stream.ofNullable(jwt.getClaimAsString(ROLE_CLAIM));

            Object rolesObject = jwt.getClaim(ROLES_CLAIM);
            Stream<String> plural = rolesObject instanceof Collection<?> roles
                ? roles.stream().filter(String.class::isInstance).map(String.class::cast)
                : Stream.empty();

            return Stream.concat(singular, plural)
                .filter(Objects::nonNull)
                .map(String::trim)
                .filter(role -> !role.isEmpty())
                .map(role -> new SimpleGrantedAuthority(ROLE_PREFIX + role.toUpperCase(Locale.ROOT)))
                .collect(Collectors.toSet());

        } catch (Exception e) {
            // A malformed claim must not break authentication — the user simply gets no roles
            // and will be rejected by the authorization rules with a 403.
            System.err.println("Error extracting roles from JWT: " + e.getMessage());
            return Collections.emptyList();
        }
    }
}
