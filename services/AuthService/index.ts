import express from "express";
import session from "express-session";

const app = express();
const port = process.env.PORT || 8080;

app.use(session({ 
    secret: 'abc123',
    cookie: { secure: false }
 }));

app.get('/auth', (req, res) => {
    res.send('This is the authentication service. You now have a session and a cookie');
});

app.listen(port, () => console.log(`App listening on PORT ${port}`));