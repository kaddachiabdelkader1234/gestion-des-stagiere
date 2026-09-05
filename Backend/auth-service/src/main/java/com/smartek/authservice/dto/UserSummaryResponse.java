package com.smartek.authservice.dto;

import com.smartek.authservice.enums.RoleType;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

/**
 * Minimal user record for pickers such as the admin's "assign an encadrant" dropdown.
 *
 * Deliberately not {@link AuthResponse}: that type carries a {@code token} field and a base64
 * profile image, neither of which belongs in a list response — one would be a credential leak
 * waiting to happen, the other would bloat the payload.
 */
@Data
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class UserSummaryResponse {

    private Long userId;
    private String email;
    private String firstName;
    private RoleType role;
}
