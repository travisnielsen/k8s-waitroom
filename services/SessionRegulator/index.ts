import express from "express";

const app = express();
const port = process.env.PORT || 8080;

app.get('/', (req, res) => {
    res.send('Hello from express and typescript');
});

app.listen(port, () => console.log(`App listening on PORT ${port}`));