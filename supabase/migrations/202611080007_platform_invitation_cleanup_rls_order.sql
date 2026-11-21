DROP POLICY memberships_revoke_undelivered_invitation
    ON security.memberships;

CREATE POLICY memberships_revoke_undelivered_invitation
    ON security.memberships
    FOR UPDATE
    TO elangi_runtime
    USING (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Pending'
        AND user_profile_id IS NULL
        AND NOT is_default_company
        AND EXISTS (
            SELECT 1
            FROM security.invitations AS invitation
            JOIN security.user_profiles AS profile
                ON profile.id = invitation.created_by_platform_profile_id
            WHERE invitation.company_id = memberships.company_id
              AND invitation.membership_id = memberships.id
              AND invitation.status = 'Revoked'
              AND profile.auth_user_id =
                  nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
    )
    WITH CHECK (
        current_setting('app.is_platform_owner', true) = 'true'
        AND status = 'Revoked'
        AND user_profile_id IS NULL
        AND NOT is_default_company
        AND EXISTS (
            SELECT 1
            FROM security.invitations AS invitation
            JOIN security.user_profiles AS profile
                ON profile.id = invitation.created_by_platform_profile_id
            WHERE invitation.company_id = memberships.company_id
              AND invitation.membership_id = memberships.id
              AND invitation.status = 'Revoked'
              AND profile.auth_user_id =
                  nullif(current_setting('app.current_actor_id', true), '')::uuid
        )
    );
