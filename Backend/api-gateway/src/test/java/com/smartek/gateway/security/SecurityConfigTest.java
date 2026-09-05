package com.smartek.gateway.security;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.reactive.AutoConfigureWebTestClient;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.http.MediaType;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.test.web.reactive.server.WebTestClient;

import static org.springframework.security.test.web.reactive.server.SecurityMockServerConfigurers.mockJwt;

/**
 * Security configuration tests for API Gateway.
 *
 * Routes point at lb:// URIs with discovery disabled in tests, so a request that passes
 * authorization fails later at routing. These tests therefore assert only on the security
 * outcome: 401 (unauthenticated), 403 (wrong role), or "neither" (authorized).
 */
@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
@AutoConfigureWebTestClient
class SecurityConfigTest {

    @Autowired
    private WebTestClient webTestClient;

    @Test
    void healthEndpoint_shouldBeAccessible_withoutToken() {
        webTestClient.get()
            .uri("/actuator/health")
            .exchange()
            .expectStatus().isOk();
    }

    @Test
    void infoEndpoint_shouldBeAccessible_withoutToken() {
        webTestClient.get()
            .uri("/actuator/info")
            .exchange()
            .expectStatus().isOk();
    }

    @Test
    void authRoute_shouldBePublic() {
        webTestClient.post()
            .uri("/api/v1/auth/login")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status ->
                org.assertj.core.api.Assertions.assertThat(status)
                    .as("auth endpoints must not require a token")
                    .isNotIn(401, 403));
    }

    @Test
    void authRegister_shouldBePublic() {
        webTestClient.post()
            .uri("/api/v1/auth/register")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "registration must be open"));
    }

    @Test
    void authUserLookup_shouldReturn401_withoutToken() {
        // Returns email / role / profile image for a guessable numeric id — must never be
        // anonymously readable, or the whole user table is enumerable.
        webTestClient.get()
            .uri("/api/v1/auth/user/1")
            .exchange()
            .expectStatus().isUnauthorized();
    }

    @Test
    void authUserLookup_shouldReturn403_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .get()
            .uri("/api/v1/auth/user/1")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void authUserLookup_shouldBeAllowed_forAdmin() {
        webTestClient
            .mutateWith(jwtWithRole("ADMIN"))
            .get()
            .uri("/api/v1/auth/user/1")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "ADMIN may look up a user"));
    }

    @Test
    void authValidate_shouldReturn401_withoutToken() {
        webTestClient.get()
            .uri("/api/v1/auth/validate/1")
            .exchange()
            .expectStatus().isUnauthorized();
    }

    @Test
    void authUserList_shouldReturn403_forTrainer() {
        // /users?role=… returns whole account records. Without an explicit rule this fell through
        // to .authenticated(), letting any logged-in user enumerate the user table.
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .get()
            .uri("/api/v1/auth/users?role=TRAINER")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void authUserList_shouldBeAllowed_forAdmin() {
        webTestClient
            .mutateWith(jwtWithRole("ADMIN"))
            .get()
            .uri("/api/v1/auth/users?role=TRAINER")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "ADMIN populates the encadrant dropdown"));
    }

    @Test
    void candidatureSubmit_shouldBeAllowed_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .post()
            .uri("/api/v1/candidatures")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "LEARNER may submit a candidature"));
    }

    @Test
    void candidatureSubmit_shouldReturn403_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .post()
            .uri("/api/v1/candidatures")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void candidatureAccept_shouldReturn403_forLearner() {
        // A learner must never be able to approve their own application.
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .post()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111/accepter")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void candidatureAccept_shouldReturn403_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .post()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111/accepter")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void candidatureAccept_shouldBeAllowed_forAdmin() {
        webTestClient
            .mutateWith(jwtWithRole("ADMIN"))
            .post()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111/accepter")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "ADMIN may accept a candidature"));
    }

    @Test
    void candidatureRead_shouldBeAllowed_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .get()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "TRAINER may read a candidature"));
    }

    @Test
    void candidatureDocumentUpload_shouldBeAllowed_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .post()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111/document")
            .contentType(MediaType.MULTIPART_FORM_DATA)
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "LEARNER may upload a document"));
    }

    @Test
    void candidatureDocumentUpload_shouldReturn403_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .post()
            .uri("/api/v1/candidatures/11111111-1111-1111-1111-111111111111/document")
            .contentType(MediaType.MULTIPART_FORM_DATA)
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void candidatureRead_shouldReturn401_withoutToken() {
        webTestClient.get()
            .uri("/api/v1/candidatures")
            .exchange()
            .expectStatus().isUnauthorized();
    }

    @Test
    void stagiairesEndpoint_shouldReturn401_withoutToken() {
        webTestClient.get()
            .uri("/api/v1/stagiaires")
            .exchange()
            .expectStatus().isUnauthorized();
    }

    @Test
    void conventionsEndpoint_shouldReturn401_withoutToken() {
        webTestClient.get()
            .uri("/api/v1/conventions")
            .exchange()
            .expectStatus().isUnauthorized();
    }

    @Test
    void stagiairesRead_shouldBeAllowed_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .get()
            .uri("/api/v1/stagiaires")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "LEARNER may read stagiaires"));
    }

    @Test
    void stagiairesRead_shouldBeAllowed_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .get()
            .uri("/api/v1/stagiaires")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "TRAINER may read stagiaires"));
    }

    @Test
    void stagiaireDelete_shouldReturn403_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .delete()
            .uri("/api/v1/stagiaires/11111111-1111-1111-1111-111111111111")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void stagiaireDelete_shouldBeAllowed_forAdmin() {
        webTestClient
            .mutateWith(jwtWithRole("ADMIN"))
            .delete()
            .uri("/api/v1/stagiaires/11111111-1111-1111-1111-111111111111")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "ADMIN may delete a stagiaire"));
    }

    @Test
    void conventionWrite_shouldReturn403_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .post()
            .uri("/api/v1/conventions")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void conventionWrite_shouldBeAllowed_forAdmin() {
        webTestClient
            .mutateWith(jwtWithRole("ADMIN"))
            .post()
            .uri("/api/v1/conventions")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "ADMIN may create a convention"));
    }

    @Test
    void evaluationWrite_shouldBeAllowed_forTrainer() {
        webTestClient
            .mutateWith(jwtWithRole("TRAINER"))
            .post()
            .uri("/api/v1/evaluations")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "TRAINER may submit an evaluation"));
    }

    @Test
    void evaluationWrite_shouldReturn403_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .post()
            .uri("/api/v1/evaluations")
            .contentType(MediaType.APPLICATION_JSON)
            .bodyValue("{}")
            .exchange()
            .expectStatus().isForbidden();
    }

    @Test
    void evaluationRead_shouldBeAllowed_forLearner() {
        webTestClient
            .mutateWith(jwtWithRole("LEARNER"))
            .get()
            .uri("/api/v1/evaluations")
            .exchange()
            .expectStatus().value(status -> assertAuthorized(status, "LEARNER may read their evaluation"));
    }

    /**
     * Builds a mock JWT carrying the flat {@code role} claim that auth-service emits,
     * exercising {@link JwtAuthenticationConverter} rather than hardcoding authorities.
     */
    private static org.springframework.test.web.reactive.server.WebTestClientConfigurer jwtWithRole(String role) {
        return mockJwt()
            .jwt(jwt -> jwt.claim("role", role))
            .authorities(new SimpleGrantedAuthority("ROLE_" + role));
    }

    /**
     * Passing authorization means the request was neither rejected as unauthenticated (401)
     * nor as forbidden (403). Any later routing failure is expected and irrelevant here.
     */
    private static void assertAuthorized(int status, String description) {
        org.assertj.core.api.Assertions.assertThat(status)
            .as(description)
            .isNotIn(401, 403);
    }
}
