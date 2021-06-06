//import decode from '@auth0/auth0-spa-js/dist/auth0-spa-js.production';
import decode from '@auth0/auth0-spa-js';
import createAuth0Client from '@auth0/auth0-spa-js';

let auth0 = null;

const configureClient = async (args) => {
  auth0 = await createAuth0Client({
    domain: args[0],
    client_id: args[1]
  });
};

const isAuthenticated = async () => { 
  const authResult = await auth0.isAuthenticated();
  return authResult;
};

const login = async (targetUrl) => {
  try {
    const options = {
      redirect_uri: window.location.origin,
      scope: "openid email profile",
      response_type: "token id_token",
    };

    if (targetUrl) {
      options.appState = { targetUrl };
    }

    // todo: try LoginWithPopup
    return await auth0.loginWithRedirect(options);
  } catch (err) {
    console.log("Log in failed", err);
  }
};


const getUser = async () => {
  return await auth0.getUser({
    scope: 'openid profile email'
  });
};

const logout = async () => {
  try {
    await auth0.logout({
      returnTo: window.location.origin
    });
  } catch (err) {
    console.log("Log out failed", err);
  }
};

const handleRedirect = async() => {
  const result = await auth0.handleRedirectCallback();
  window.history.replaceState({}, document.title, "/");
}

const getToken = async() => {
  const tokenStr = await auth0.getTokenWithPopup({
    scope: 'email',
    audience: 'http://localhost:5000'
  }, {
    timeoutInSeconds: 90
  });
  return tokenStr;
}

export {
  configureClient,
  login,
  isAuthenticated,
  getUser,
  logout,
  handleRedirect,
  getToken
};