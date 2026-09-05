package com.smartek.authservice.repository;

import com.smartek.authservice.entity.User;
import com.smartek.authservice.enums.RoleType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;
import org.springframework.stereotype.Repository;

import java.util.List;
import java.util.Optional;

@Repository
public interface UserRepository extends JpaRepository<User, Long> {
    
    Optional<User> findByEmail(String email);
    
    Boolean existsByEmail(String email);
    
    List<User> findByRole(RoleType role);
    
    Long countByRole(RoleType role);
    
    Boolean existsByRole(RoleType role);

    /**
     * Atomically checks that the stored refresh token matches and replaces it with a new one.
     * Returns the number of rows affected (1 = success, 0 = token was already used or doesn't exist).
     *
     * This replaces the read-check-then-write pattern which had a TOCTOU race: two concurrent
     * requests could both read the old token, both pass the check, and both write new tokens —
     * effectively allowing a refresh token to be reused.
     */
    // NOTE: The atomic swap is now done via JdbcTemplate in AuthService to bypass Hibernate's
    // persistence context entirely. See AuthService.refresh() for the implementation.
}
