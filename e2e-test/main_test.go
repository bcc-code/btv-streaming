package e2etest_test

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"
	"net/url"
	"os"
	"strings"
	"testing"
	"time"

	"github.com/lestrrat-go/jwx/jwt"
	"github.com/stretchr/testify/assert"
)

var username = ""
var password = ""
var clientID = ""
var clientSecret = ""

func init() {
	username = url.QueryEscape(os.Getenv("TEST_USERNAME"))
	password = url.QueryEscape(os.Getenv("TEST_PASSWORD"))
	clientID = url.QueryEscape(os.Getenv("TEST_CLIENT_ID"))
	clientSecret = url.QueryEscape(os.Getenv("TEST_CLIENT_SECRET"))
}

type tokenRes struct {
	AccessToken string `json:"access_token"`
}

func getAuthToken() (string, error) {
	urlBase := "https://login.bcc.no/oauth/token"
	audience := url.QueryEscape("https://widgets.brunstad.org")

	query := fmt.Sprintf("grant_type=password&username=%s&password=%s&audience=%s&scope=openid,profil&client_id=%s&client_secret=%s", username, password, audience, clientID, clientSecret)

	payload := strings.NewReader(query)
	req, err := http.NewRequest("POST", urlBase, payload)

	if err != nil {
		return "", err
	}

	req.Header.Add("content-type", "application/x-www-form-urlencoded")
	res, err := http.DefaultClient.Do(req)
	if err != nil {
		return "", err
	}

	defer res.Body.Close()
	body, err := ioutil.ReadAll(res.Body)
	if err != nil {
		return "", err
	}

	parsed := &tokenRes{}
	err = json.Unmarshal(body, parsed)
	if err != nil {
		return "", err
	}

	return parsed.AccessToken, nil
}

type liveUrlsRes struct {
	URL        string `json:"url"`
	ExpiryTime string `json:"expiryTime"`
}

func TestURLFlow_Cmaf(t *testing.T) {

	token, err := getAuthToken()
	assert.NoError(t, err)

	link := os.Getenv("URLDELIVERY_URL")

	req, err := http.NewRequest("GET", link, nil)
	assert.NoError(t, err)
	req.Header.Add("Authorization", fmt.Sprintf("Bearer %s", token))
	res, err := http.DefaultClient.Do(req)
	assert.NoError(t, err)
	defer res.Body.Close()
	body, err := ioutil.ReadAll(res.Body)
	assert.NoError(t, err)

	urlRes := &liveUrlsRes{}
	err = json.Unmarshal(body, urlRes)

	assert.NoError(t, err)
	assert.NotEmpty(t, urlRes)
	assert.NotEmpty(t, urlRes.ExpiryTime)
	assert.NotEmpty(t, urlRes.URL)

	parsedURL, err := url.Parse(urlRes.URL)
	assert.NoError(t, err)
	assert.Empty(t, parsedURL.Fragment)
	assert.Equal(t, os.Getenv("PROXY_HOST"), parsedURL.Host)
	assert.Equal(t, "/api/cmaf-proxy/top-level", parsedURL.Path)
	assert.Equal(t, "https", parsedURL.Scheme)

	q := parsedURL.Query()
	assert.True(t, q.Has("url"))
	parsedInnerURL, err := url.Parse(q.Get("url"))
	assert.NoError(t, err)
	assert.Empty(t, parsedInnerURL.Fragment)
	assert.Equal(t, os.Getenv("STREAM_HOST"), parsedInnerURL.Host)
	assert.Regexp(t, os.Getenv("TEST_REGEX_INNER_PATH_1"), parsedInnerURL.Path)
	assert.Equal(t, "https", parsedInnerURL.Scheme)

	assert.True(t, q.Has("token"))
	parsedToken, err := jwt.Parse([]byte(q.Get("token")))
	assert.NoError(t, err)
	assert.Equal(t, []string{"urn:brunstadtv"}, parsedToken.Audience())
	assert.True(t, parsedToken.Expiration().After(time.Now().Add(5*time.Hour)))
	assert.True(t, parsedToken.IssuedAt().Before(time.Now()))
	assert.Equal(t, "https://brunstad.tv", parsedToken.Issuer())

	// Validate that that's *all* we have
	q.Del("token")
	q.Del("url")
	assert.Equal(t, url.Values{}, q)

	q2 := parsedInnerURL.Query()
	assert.True(t, q2.Has("Signature"))
	assert.True(t, q2.Has("Policy"))
	assert.True(t, q2.Has("Key-Pair-Id"))

	// Validate that that's *all* we have
	q2.Del("Policy")
	q2.Del("Signature")
	q2.Del("Key-Pair-Id")
	assert.Equal(t, url.Values{}, q2)

	manifestRes, err := http.Get(parsedURL.Query().Get("url"))
	assert.NoError(t, err)

	defer manifestRes.Body.Close()
	manifest, err := ioutil.ReadAll(manifestRes.Body)
	assert.NoError(t, err)
	assert.True(t, strings.HasPrefix(string(manifest), "#EXTM3U"))
}
