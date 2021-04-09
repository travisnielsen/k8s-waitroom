import express from "express";

const app = express();
const port = process.env.PORT || 8080;

app.get('/', (req, res) => {
    res.send('Hello from express and typescript');

    // TODO: Look at this as a possibility for request forwarding to the AuthService
    // https://stackoverflow.com/questions/32140987/how-to-forward-a-request-to-other-endpoint-in-node-js

});

app.listen(port, () => console.log(`App listening on PORT ${port}`));