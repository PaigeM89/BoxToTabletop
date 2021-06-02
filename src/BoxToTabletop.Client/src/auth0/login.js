// const configureClient = async () => {
//   const response = await fetchAuthConfig();
//   const config = await response.json();

//   auth0 = await createAuth0Client({
//     domain: config.domain,
//     client_id: config.clientId
//   });
// };

// import * as auth0 from "@auth0/auth0-spa-js";
import createAuth0Client from '@auth0/auth0-spa-js';

let auth0 = null;

const configureClient = async (args) => {
  console.log('domain and client id', args[0], args[1]);
  auth0 = await createAuth0Client({
    domain: args[0],
    client_id: args[1]
  });
};

const isAuthenticated = async () => { 
  console.log('auth0 while checking auth is ', auth0);
  const authResult = await auth0.isAuthenticated();
  console.log('Authentication result is ', authResult);
  return authResult;
};

const login = async (targetUrl) => {
  try {
    console.log("Logging in", targetUrl);
    console.log('auth0 during login is ', auth0);

    const options = {
      redirect_uri: window.location.origin,
      scope: "openid email current_user_metadata user_metadata"
    };

    if (targetUrl) {
      options.appState = { targetUrl };
    }

    console.log('attempting login with target url and options', targetUrl, options);

    // todo: try LoginWithPopup
    return await auth0.loginWithRedirect(options);
  } catch (err) {
    console.log("Log in failed", err);
  }
};

const getClaims = async() => {
  let opts = {
    scope: "read:current_user"
  };
  let claims = await auth0.getIdTokenClaims(opts);
  console.log('claims are ', claims);
  return claims;
};

const getUser = async () => {
  console.log('auth0 while checking user is ', auth0);
  // const user = await auth0.getUser({
  //   scope: "openid profile email",
  //   audience: ""
  // });
  const claims = await getClaims();
  const user = await auth0.getUser();
  console.log('user in javascript is ', user);
  return user;
};

const logout = async () => {
  try {
    console.log('logging out');
    auth0.logout({
      returnTo: window.location.origin
    });
  } catch (err) {
    console.log("Log out failed", err);
  }
};

const handleRedirect = async() => {
  const result = await auth0.handleRedirectCallback();
  console.log('user is logged in! Result: ', result);
  window.history.replaceState({}, document.title, "/");
}

export {
  configureClient,
  login,
  isAuthenticated,
  getUser,
  logout,
  handleRedirect
};