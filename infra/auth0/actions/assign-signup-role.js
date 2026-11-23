// Auth0 Post-Login Action. Assigns the role a new user chose at sign-up.
// Paste into an Action on the Login / Post Login trigger, add the `auth0`
// npm dependency, and set the secrets listed in docs/auth0-agent-handoff.md.

const { ManagementClient } = require('auth0');

// Criteria for self-selected roles: requested_role value -> Action secret holding the Auth0 role ID.
// Admin is deliberately absent; an administrator grants it.
const SELF_SELECTABLE_ROLES = new Map([['pro-chef', 'PRO_CHEF_ROLE_ID']]);

exports.onExecutePostLogin = async (event) => {
  // Only the first login is a sign-up; existing users keep their roles.
  if (event.stats.logins_count !== 1) return;

  const requestedRole = event.request.query?.requested_role;
  const secretName = SELF_SELECTABLE_ROLES.get(requestedRole);
  if (!secretName) return;

  const management = new ManagementClient({
    domain: event.secrets.domain,
    clientId: event.secrets.clientId,
    clientSecret: event.secrets.clientSecret,
  });

  // A failed assignment leaves the user with the default User role.
  try {
    await management.users.roles.assign(event.user.user_id, { roles: [event.secrets[secretName]] });
  } catch (error) {
    console.error('Sign-up role assignment failed', error.message);
  }
};
